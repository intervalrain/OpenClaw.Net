using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Execution engine that uses the Pioneer Agent to plan a DAG,
/// then executes it via the DagExecutor.
/// This is the "DAG" strategy of IExecutionEngine, opt-in alongside SimpleExecutionEngine.
/// </summary>
public class DagExecutionEngine(
    IDagExecutor dagExecutor,
    IAgentRegistry agentRegistry,
    IServiceProvider serviceProvider,
    ILogger<DagExecutionEngine> logger) : IExecutionEngine
{
    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct = default)
    {
        // Step 1: Plan — use Pioneer to generate DAG
        logger.LogInformation("DagExecutionEngine: planning via Pioneer...");

        var pioneer = agentRegistry.GetAgent("pioneer");
        if (pioneer is null)
            return ExecutionResult.Failure("Pioneer agent not found in registry.");

        var planContext = new AgentExecutionContext
        {
            Input = JsonDocument.Parse(JsonSerializer.Serialize(new { task = request.Content })),
            Services = serviceProvider,
            Options = new AgentExecutionOptions
            {
                MaxDepth = 1,
                MaxIterations = 1,
                Timeout = TimeSpan.FromMinutes(2)
            }
        };

        var planResult = await pioneer.ExecuteAsync(planContext, ct);
        if (planResult.Status != AgentResultStatus.Success)
        {
            logger.LogWarning("Pioneer planning failed: {Error}", planResult.ErrorMessage);
            return ExecutionResult.Failure($"Planning failed: {planResult.ErrorMessage}");
        }

        // Step 2: Extract the graph from Pioneer's output
        TaskGraph? graph;
        try
        {
            var graphElement = planResult.Output.RootElement.GetProperty("graph");
            graph = TaskGraphSerializer.DeserializeJson(graphElement.GetRawText());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract graph from Pioneer output");
            return ExecutionResult.Failure("Failed to extract graph from Pioneer output.");
        }

        if (graph is null)
            return ExecutionResult.Failure("Pioneer output did not contain a valid graph.");

        logger.LogInformation("DagExecutionEngine: executing DAG '{Name}' with {Nodes} nodes",
            graph.Name, graph.Nodes.Count);

        // Step 3: Execute the DAG
        var dagResult = await dagExecutor.ExecuteAsync(graph, new AgentExecutionOptions
        {
            MaxDepth = 5,
            MaxIterations = 10,
            Timeout = TimeSpan.FromMinutes(10)
        }, ct);

        // Step 4: Aggregate results
        var nodeOutputs = graph.Nodes
            .Where(n => n.Status == TaskNodeStatus.Completed && n.Output is not null)
            .ToDictionary(n => n.Id, n => n.Output!.RootElement.Clone());

        var outputJson = JsonSerializer.Serialize(new
        {
            workflow = graph.Name,
            success = dagResult.IsSuccess,
            totalTokensUsed = dagResult.TotalTokensUsed,
            nodes = graph.Nodes.Select(n => new
            {
                id = n.Id,
                agent = n.AgentName,
                status = n.Status.ToString(),
                error = n.ErrorMessage
            })
        });

        if (dagResult.IsSuccess)
        {
            return ExecutionResult.Success(outputJson);
        }

        return new ExecutionResult
        {
            IsSuccess = false,
            Output = outputJson,
            ErrorMessage = dagResult.ErrorMessage
        };
    }
}
