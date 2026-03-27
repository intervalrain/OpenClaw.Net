using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Application.Skills;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Workflows;

public interface IWorkflowExecutor
{
    /// <summary>
    /// Starts workflow execution. Returns immediately with execution ID.
    /// The workflow runs in background.
    /// </summary>
    Task<Guid> StartAsync(
        WorkflowDefinition workflow,
        string? inputJson,
        Guid? userId,
        ExecutionTrigger trigger,
        CancellationToken ct = default);

    /// <summary>
    /// Resumes workflow after approval decision.
    /// </summary>
    Task ResumeAfterApprovalAsync(
        Guid executionId,
        string nodeId,
        bool approved,
        string? editedOutput = null,
        CancellationToken ct = default);
}

public class WorkflowExecutor(
    IServiceScopeFactory scopeFactory,
    IWorkflowExecutionStore approvalStore,
    ILogger<WorkflowExecutor> logger) : IWorkflowExecutor
{
    public async Task<Guid> StartAsync(
        WorkflowDefinition workflow,
        string? inputJson,
        Guid? userId,
        ExecutionTrigger trigger,
        CancellationToken ct = default)
    {
        // Create execution record
        using var scope = scopeFactory.CreateScope();
        var executionRepo = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var execution = WorkflowExecution.Create(workflow.Id, userId, trigger, inputJson);
        await executionRepo.AddAsync(execution, ct);
        await uow.SaveChangesAsync(ct);

        var executionId = execution.Id;

        // Parse workflow graph
        var graph = JsonSerializer.Deserialize<WorkflowGraph>(
            workflow.DefinitionJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to parse workflow definition");

        // Start background execution (do NOT use the request's CancellationToken —
        // it cancels when the HTTP response is sent, killing the background work)
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteGraphAsync(executionId, graph, userId, trigger, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Workflow execution {ExecutionId} failed", executionId);
                await FailExecutionAsync(executionId, ex.Message);
            }
        });

        return executionId;
    }

    public async Task ResumeAfterApprovalAsync(
        Guid executionId,
        string nodeId,
        bool approved,
        string? editedOutput = null,
        CancellationToken ct = default)
    {
        await approvalStore.SubmitApprovalAsync(executionId, nodeId, approved, editedOutput, ct);
    }

    private async Task ExecuteGraphAsync(
        Guid executionId,
        WorkflowGraph graph,
        Guid? userId,
        ExecutionTrigger trigger,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var executionRepo = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
        var skillRegistry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        var skillStore = scope.ServiceProvider.GetRequiredService<ISkillStore>();
        var llmProviderFactory = scope.ServiceProvider.GetRequiredService<ILlmProviderFactory>();
        var argResolver = scope.ServiceProvider.GetRequiredService<ArgResolver>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var execution = await executionRepo.GetByIdWithNodesAsync(executionId, ct)
            ?? throw new InvalidOperationException($"Execution {executionId} not found");

        execution.Start();
        await uow.SaveChangesAsync(ct);

        // Build adjacency list and in-degree map for topological sort
        var adjacency = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();
        var nodeMap = graph.Nodes.ToDictionary(n => n.Id);

        foreach (var node in graph.Nodes)
        {
            adjacency[node.Id] = [];
            inDegree[node.Id] = 0;
        }

        foreach (var edge in graph.Edges)
        {
            adjacency[edge.Source].Add(edge.Target);
            inDegree[edge.Target]++;
        }

        // Track completed node outputs and inputs
        var nodeOutputs = new Dictionary<string, string>();
        var nodeInputs = new Dictionary<string, string>();
        var completedNodes = new HashSet<string>();

        // Create node executions for all nodes
        foreach (var node in graph.Nodes)
        {
            execution.AddNodeExecution(node.Id);
        }
        await uow.SaveChangesAsync(ct);

        // Determine which nodes require wait-all (Wait, End) vs wait-any (everything else)
        var waitAllNodes = new HashSet<string>(
            graph.Nodes.Where(n => n is WaitNode or EndNode).Select(n => n.Id));

        // Also track original in-degrees for wait-any detection
        var originalInDegree = new Dictionary<string, int>(inDegree);

        // Track which nodes are already enqueued to prevent duplicates
        var enqueuedNodes = new HashSet<string>();

        // Execute using modified Kahn's algorithm:
        // - Wait/End nodes: fire when ALL parents complete (in-degree == 0)
        // - Other nodes: fire when ANY parent completes (in-degree < original)
        var readyNodes = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        foreach (var id in readyNodes) enqueuedNodes.Add(id);

        while (readyNodes.Count > 0)
        {
            var batch = new List<string>();
            while (readyNodes.Count > 0)
            {
                batch.Add(readyNodes.Dequeue());
            }

            var tasks = batch.Select(async nodeId =>
            {
                var node = nodeMap[nodeId];
                var nodeExecution = execution.NodeExecutions.First(ne => ne.NodeId == nodeId);

                try
                {
                    var output = await ExecuteNodeAsync(
                        node,
                        nodeExecution,
                        execution,
                        nodeOutputs,
                        nodeInputs,
                        userId,
                        trigger,
                        skillRegistry,
                        skillStore,
                        llmProviderFactory,
                        argResolver,
                        uow,
                        ct);

                    if (output is not null)
                    {
                        nodeOutputs[nodeId] = output;
                    }

                    completedNodes.Add(nodeId);
                    return (nodeId, success: true);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (WorkflowRejectedException)
                {
                    return (nodeId, success: false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Node {NodeId} failed in execution {ExecutionId}", nodeId, executionId);
                    nodeExecution.Fail(ex.Message);
                    return (nodeId, success: false);
                }
            });

            var results = await Task.WhenAll(tasks);
            await uow.SaveChangesAsync(ct);

            // Check if any node failed
            var failedNode = results.FirstOrDefault(r => !r.success);
            if (failedNode != default)
            {
                SkipDownstreamNodes(failedNode.nodeId, adjacency, nodeMap, execution);
                await uow.SaveChangesAsync(ct);

                execution.Fail($"Node {failedNode.nodeId} failed");
                await uow.SaveChangesAsync(ct);
                return;
            }

            // Update in-degrees and find newly ready nodes
            foreach (var (nodeId, _) in results)
            {
                foreach (var neighbor in adjacency[nodeId])
                {
                    inDegree[neighbor]--;

                    // Already enqueued? Skip
                    if (enqueuedNodes.Contains(neighbor)) continue;

                    bool shouldFire;
                    if (waitAllNodes.Contains(neighbor))
                    {
                        // Wait-all: fire only when ALL parents complete
                        shouldFire = inDegree[neighbor] == 0;
                    }
                    else
                    {
                        // Wait-any: fire when ANY parent completes
                        shouldFire = inDegree[neighbor] < originalInDegree[neighbor];
                    }

                    if (shouldFire)
                    {
                        readyNodes.Enqueue(neighbor);
                        enqueuedNodes.Add(neighbor);
                    }
                }
            }
        }

        // Mark execution as completed with combined outputs
        var executionOutput = nodeOutputs.Count > 0
            ? JsonSerializer.Serialize(nodeOutputs.ToDictionary(
                kv => kv.Key,
                kv => (object?)JsonHelper.ParseJsonSafe(kv.Value)))
            : null;
        execution.Complete(executionOutput);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<string?> ExecuteNodeAsync(
        WorkflowNode node,
        WorkflowNodeExecution nodeExecution,
        WorkflowExecution execution,
        Dictionary<string, string> nodeOutputs,
        Dictionary<string, string> nodeInputs,
        Guid? userId,
        ExecutionTrigger trigger,
        IToolRegistry skillRegistry,
        ISkillStore skillStore,
        ILlmProviderFactory llmProviderFactory,
        ArgResolver argResolver,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        nodeExecution.Start();

        switch (node)
        {
            case StartNode:
                nodeExecution.Complete();
                return null;

            case WaitNode:
            {
                // Merge all upstream outputs as this node's output
                var upstreamOutputs = new Dictionary<string, object?>();
                foreach (var (id, output) in nodeOutputs)
                {
                    try { upstreamOutputs[id] = JsonSerializer.Deserialize<object>(output); }
                    catch { upstreamOutputs[id] = output; }
                }
                var merged = JsonSerializer.Serialize(upstreamOutputs);
                nodeExecution.Complete(merged);
                return merged;
            }

            case EndNode:
            {
                // End collects all outputs as final result
                var allOutputs = new Dictionary<string, object?>();
                foreach (var (id, output) in nodeOutputs)
                {
                    try { allOutputs[id] = JsonSerializer.Deserialize<object>(output); }
                    catch { allOutputs[id] = output; }
                }
                var finalOutput = JsonSerializer.Serialize(allOutputs);
                nodeExecution.Complete(finalOutput);
                return finalOutput;
            }

            case SkillNode skillNode:
                return await ExecuteSkillNodeAsync(
                    skillNode,
                    nodeExecution,
                    nodeOutputs,
                    nodeInputs,
                    userId,
                    skillRegistry,
                    skillStore,
                    llmProviderFactory,
                    argResolver,
                    ct);

            case ApprovalNode approvalNode:
                return await ExecuteApprovalNodeAsync(
                    approvalNode,
                    nodeExecution,
                    execution,
                    trigger,
                    uow,
                    ct);

            default:
                nodeExecution.Complete();
                return null;
        }
    }

    private async Task<string?> ExecuteSkillNodeAsync(
        SkillNode node,
        WorkflowNodeExecution nodeExecution,
        Dictionary<string, string> nodeOutputs,
        Dictionary<string, string> nodeInputs,
        Guid? userId,
        IToolRegistry toolRegistry,
        ISkillStore skillStore,
        ILlmProviderFactory llmProviderFactory,
        ArgResolver argResolver,
        CancellationToken ct)
    {
        // Check if this is a Markdown Skill or a direct Tool
        var mdSkill = skillStore.GetSkill(node.SkillName);
        if (mdSkill is not null)
        {
            return await ExecuteMarkdownSkillAsync(
                node, mdSkill, nodeExecution, nodeOutputs, toolRegistry, llmProviderFactory, ct);
        }

        // Fallback: direct Tool execution
        var tool = toolRegistry.GetSkill(node.SkillName)
            ?? throw new InvalidOperationException($"Skill/Tool '{node.SkillName}' not found");

        var argsJson = await argResolver.ResolveArgsJsonAsync(
            node, tool, nodeOutputs, nodeInputs, userId, ct);

        nodeInputs[node.Id] = argsJson;
        nodeExecution.Start(argsJson);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(node.TimeoutSeconds));

        var context = new ToolContext(argsJson);
        var result = await tool.ExecuteAsync(context, cts.Token);

        if (result.IsSuccess)
        {
            nodeExecution.Complete(result.Output);
            return result.Output;
        }

        nodeExecution.Fail(result.Error ?? "Tool execution failed");
        throw new InvalidOperationException(result.Error ?? "Tool execution failed");
    }

    /// <summary>
    /// Executes a Markdown-defined Skill by calling LLM with the skill's instructions
    /// as system prompt, upstream outputs as context, and the skill's tools available.
    /// Runs a tool-use loop until the LLM returns a final text response.
    /// </summary>
    private async Task<string?> ExecuteMarkdownSkillAsync(
        SkillNode node,
        ISkill skill,
        WorkflowNodeExecution nodeExecution,
        Dictionary<string, string> nodeOutputs,
        IToolRegistry toolRegistry,
        ILlmProviderFactory llmProviderFactory,
        CancellationToken ct)
    {
        var llmProvider = await llmProviderFactory.GetProviderAsync(ct);

        // Build available tools from the skill's tool list
        var toolDefs = new List<ToolDefinition>();
        var toolMap = new Dictionary<string, IAgentTool>(StringComparer.OrdinalIgnoreCase);
        foreach (var toolName in skill.Tools)
        {
            var tool = toolRegistry.GetSkill(toolName);
            if (tool is null)
            {
                logger.LogWarning("Skill {Skill} references unknown tool {Tool}", skill.Name, toolName);
                continue;
            }
            toolDefs.Add(new ToolDefinition(tool.Name, tool.Description, tool.Parameters));
            toolMap[tool.Name] = tool;
        }

        // Build upstream context
        var upstreamContext = nodeOutputs.Count > 0
            ? string.Join("\n\n", nodeOutputs.Select(kv =>
                $"=== Output from '{kv.Key}' ===\n{kv.Value}"))
            : "No upstream data available.";

        // Additional context from node config
        var additionalContext = node.Args?.TryGetValue("skillContext", out var ctxArg) == true
            ? ctxArg.FilledValue ?? ""
            : "";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, skill.Instructions),
            new(ChatRole.User,
                $"Execute this skill with the following context:\n\n{upstreamContext}" +
                (string.IsNullOrEmpty(additionalContext) ? "" : $"\n\nAdditional context: {additionalContext}"))
        };

        nodeExecution.Start(JsonSerializer.Serialize(new { skill = skill.Name, tools = skill.Tools }));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(node.TimeoutSeconds));

        // Tool-use loop (max 10 iterations to prevent infinite loops)
        const int maxIterations = 10;
        for (var i = 0; i < maxIterations; i++)
        {
            var response = await llmProvider.ChatAsync(messages, toolDefs, cts.Token);

            if (!response.HasToolCalls)
            {
                // LLM returned final text response
                var output = response.Content ?? "";
                nodeExecution.Complete(output);
                return output;
            }

            // Add assistant message with tool calls
            messages.Add(new ChatMessage(ChatRole.Assistant, response.Content, ToolCalls: response.ToolCalls));

            // Execute each tool call
            foreach (var toolCall in response.ToolCalls!)
            {
                string toolResult;
                if (toolMap.TryGetValue(toolCall.Name, out var tool))
                {
                    var result = await tool.ExecuteAsync(new ToolContext(toolCall.Arguments), cts.Token);
                    toolResult = result.IsSuccess ? result.Output ?? "" : $"Error: {result.Error}";
                }
                else
                {
                    toolResult = $"Error: Tool '{toolCall.Name}' not available";
                }

                messages.Add(new ChatMessage(ChatRole.Tool, toolResult, toolCall.Id));
            }
        }

        // Hit max iterations
        var lastContent = messages.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Content ?? "Max iterations reached";
        nodeExecution.Complete(lastContent);
        return lastContent;
    }

    private async Task<string?> ExecuteApprovalNodeAsync(
        ApprovalNode node,
        WorkflowNodeExecution nodeExecution,
        WorkflowExecution execution,
        ExecutionTrigger trigger,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        // Check scheduled behavior
        if (trigger == ExecutionTrigger.Scheduled)
        {
            switch (node.ScheduledBehavior)
            {
                case ApprovalBehavior.AutoApprove:
                    logger.LogInformation("Auto-approving {ApprovalName} for scheduled execution",
                        node.ApprovalName);
                    nodeExecution.Complete("auto_approved");
                    return "auto_approved";

                case ApprovalBehavior.AutoReject:
                    logger.LogInformation("Auto-rejecting {ApprovalName} for scheduled execution",
                        node.ApprovalName);
                    nodeExecution.Skip();
                    throw new WorkflowRejectedException($"Approval '{node.ApprovalName}' auto-rejected");

                case ApprovalBehavior.WaitForApproval:
                default:
                    // Continue to wait for approval
                    break;
            }
        }

        // Set pending approval
        nodeExecution.SetWaitingForApproval();
        execution.SetWaitingForApproval(node.Id);

        await approvalStore.SetPendingApprovalAsync(
            execution.Id,
            node.Id,
            node.ApprovalName,
            node.Description,
            ct);

        // Persist the WaitingForApproval status BEFORE blocking, so the API can see it
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Workflow {ExecutionId} waiting for approval at {NodeId}: {ApprovalName}",
            execution.Id, node.Id, node.ApprovalName);

        // Wait for approval (may include edited output for downstream nodes)
        var (approved, editedOutput) = await approvalStore.WaitForApprovalAsync(execution.Id, node.Id, ct);

        execution.ClearPendingApproval();

        if (approved)
        {
            var output = editedOutput ?? "approved";
            nodeExecution.Complete(output);
            return output;
        }
        else
        {
            nodeExecution.Skip();
            throw new WorkflowRejectedException($"Approval '{node.ApprovalName}' was rejected");
        }
    }

    private static void SkipDownstreamNodes(
        string failedNodeId,
        Dictionary<string, List<string>> adjacency,
        Dictionary<string, WorkflowNode> nodeMap,
        WorkflowExecution execution)
    {
        var toSkip = new Queue<string>();
        foreach (var neighbor in adjacency[failedNodeId])
        {
            toSkip.Enqueue(neighbor);
        }

        var skipped = new HashSet<string>();
        while (toSkip.Count > 0)
        {
            var nodeId = toSkip.Dequeue();
            if (skipped.Contains(nodeId)) continue;
            skipped.Add(nodeId);

            var nodeExecution = execution.NodeExecutions.FirstOrDefault(ne => ne.NodeId == nodeId);
            nodeExecution?.Skip();

            foreach (var neighbor in adjacency[nodeId])
            {
                toSkip.Enqueue(neighbor);
            }
        }
    }

    private async Task FailExecutionAsync(Guid executionId, string errorMessage)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var execution = await repo.GetByIdAsync(executionId);
            if (execution is not null)
            {
                execution.Fail(errorMessage);
                await uow.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark execution {ExecutionId} as failed", executionId);
        }
    }
}

public class WorkflowRejectedException(string message) : Exception(message);

file class JsonHelper
{
    public static object? ParseJsonSafe(string value)
    {
        try { return JsonSerializer.Deserialize<object>(value); }
        catch { return value; }
    }
}
