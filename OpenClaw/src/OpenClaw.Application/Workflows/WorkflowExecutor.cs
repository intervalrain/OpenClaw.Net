using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Application.Skills;
using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Workflows;
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

        // Start background execution
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteGraphAsync(executionId, graph, userId, trigger, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Workflow execution {ExecutionId} failed", executionId);
                await FailExecutionAsync(executionId, ex.Message);
            }
        }, ct);

        return executionId;
    }

    public async Task ResumeAfterApprovalAsync(
        Guid executionId,
        string nodeId,
        bool approved,
        CancellationToken ct = default)
    {
        await approvalStore.SubmitApprovalAsync(executionId, nodeId, approved, ct);
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
        var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();
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

        // Track completed node outputs
        var nodeOutputs = new Dictionary<string, string>();
        var completedNodes = new HashSet<string>();

        // Parse workflow variables
        var workflowVariables = graph.Variables;

        // Create node executions for all nodes
        foreach (var node in graph.Nodes)
        {
            execution.AddNodeExecution(node.Id);
        }
        await uow.SaveChangesAsync(ct);

        // Execute using Kahn's algorithm for topological order with parallel execution
        var readyNodes = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (readyNodes.Count > 0)
        {
            // Get all nodes that are ready to execute (no unmet dependencies)
            var batch = new List<string>();
            while (readyNodes.Count > 0)
            {
                batch.Add(readyNodes.Dequeue());
            }

            // Execute batch in parallel
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
                        workflowVariables,
                        userId,
                        trigger,
                        skillRegistry,
                        argResolver,
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
                    // Approval was rejected - skip downstream nodes
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
                // Skip all downstream nodes
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
                    if (inDegree[neighbor] == 0)
                    {
                        readyNodes.Enqueue(neighbor);
                    }
                }
            }
        }

        // Mark execution as completed
        execution.Complete();
        await uow.SaveChangesAsync(ct);
    }

    private async Task<string?> ExecuteNodeAsync(
        WorkflowNode node,
        WorkflowNodeExecution nodeExecution,
        WorkflowExecution execution,
        Dictionary<string, string> nodeOutputs,
        Dictionary<string, object>? workflowVariables,
        Guid? userId,
        ExecutionTrigger trigger,
        ISkillRegistry skillRegistry,
        ArgResolver argResolver,
        CancellationToken ct)
    {
        nodeExecution.Start();

        switch (node)
        {
            case StartNode:
            case EndNode:
                nodeExecution.Complete();
                return null;

            case SkillNode skillNode:
                return await ExecuteSkillNodeAsync(
                    skillNode,
                    nodeExecution,
                    nodeOutputs,
                    workflowVariables,
                    userId,
                    skillRegistry,
                    argResolver,
                    ct);

            case ApprovalNode approvalNode:
                return await ExecuteApprovalNodeAsync(
                    approvalNode,
                    nodeExecution,
                    execution,
                    trigger,
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
        Dictionary<string, object>? workflowVariables,
        Guid? userId,
        ISkillRegistry skillRegistry,
        ArgResolver argResolver,
        CancellationToken ct)
    {
        var skill = skillRegistry.GetSkill(node.SkillName)
            ?? throw new InvalidOperationException($"Skill '{node.SkillName}' not found");

        // Resolve arguments
        var argsJson = await argResolver.ResolveArgsJsonAsync(
            node,
            workflowVariables,
            nodeOutputs,
            userId,
            ct);

        nodeExecution.Start(argsJson);

        // Execute skill with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(node.TimeoutSeconds));

        var context = new SkillContext(argsJson);
        var result = await skill.ExecuteAsync(context, cts.Token);

        if (result.IsSuccess)
        {
            nodeExecution.Complete(result.Output);
            return result.Output;
        }
        else
        {
            nodeExecution.Fail(result.Error ?? "Skill execution failed");
            throw new InvalidOperationException(result.Error ?? "Skill execution failed");
        }
    }

    private async Task<string?> ExecuteApprovalNodeAsync(
        ApprovalNode node,
        WorkflowNodeExecution nodeExecution,
        WorkflowExecution execution,
        ExecutionTrigger trigger,
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

        logger.LogInformation("Workflow {ExecutionId} waiting for approval at {NodeId}: {ApprovalName}",
            execution.Id, node.Id, node.ApprovalName);

        // Wait for approval
        var approved = await approvalStore.WaitForApprovalAsync(execution.Id, node.Id, ct);

        execution.ClearPendingApproval();

        if (approved)
        {
            nodeExecution.Complete("approved");
            return "approved";
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
