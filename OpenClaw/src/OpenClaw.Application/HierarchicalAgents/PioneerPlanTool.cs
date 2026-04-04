using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

public record PioneerPlanArgs(
    [property: Description("The complex multi-step task to decompose into a DAG workflow and execute.")]
    string Task);

/// <summary>
/// Agent tool that delegates complex multi-step tasks to the Pioneer Agent for DAG planning,
/// then executes the resulting workflow via the DAG executor.
/// </summary>
public class PioneerPlanTool(IServiceScopeFactory scopeFactory) : AgentToolBase<PioneerPlanArgs>
{
    public override string Name => "pioneer_plan";

    public override string Description =>
        "Decomposes a complex multi-step task into a DAG workflow and executes it. " +
        "Use when the user's request requires multiple coordinated steps.";

    public override async Task<ToolResult> ExecuteAsync(
        PioneerPlanArgs args, ToolContext context, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PioneerPlanTool>>();
        var agentRegistry = scope.ServiceProvider.GetRequiredService<IAgentRegistry>();
        var dagExecutor = scope.ServiceProvider.GetRequiredService<IDagExecutor>();

        // Step 1: Get the Pioneer agent and plan the DAG
        var pioneer = agentRegistry.GetAgent("pioneer");
        if (pioneer is null)
            return ToolResult.Failure("Pioneer agent not found in registry.");

        logger.LogInformation("PioneerPlanTool: planning task via Pioneer agent");

        var planContext = new AgentExecutionContext
        {
            Input = JsonDocument.Parse(JsonSerializer.Serialize(new { task = args.Task })),
            Services = scope.ServiceProvider,
            Options = new AgentExecutionOptions
            {
                MaxDepth = 1,
                MaxIterations = 1,
                Timeout = TimeSpan.FromMinutes(2)
            },
            UserId = context.UserId
        };

        var planResult = await pioneer.ExecuteAsync(planContext, ct);
        if (planResult.Status != AgentResultStatus.Success)
        {
            logger.LogWarning("Pioneer planning failed: {Error}", planResult.ErrorMessage);
            return ToolResult.Failure($"Planning failed: {planResult.ErrorMessage}");
        }

        // Step 2: Parse the TaskGraph from Pioneer output
        TaskGraph? graph;
        try
        {
            var graphElement = planResult.Output.RootElement.GetProperty("graph");
            graph = TaskGraphSerializer.DeserializeJson(graphElement.GetRawText());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract graph from Pioneer output");
            return ToolResult.Failure("Failed to extract graph from Pioneer output.");
        }

        if (graph is null)
            return ToolResult.Failure("Pioneer output did not contain a valid graph.");

        // Step 3: Validate the graph
        var errors = TaskGraphValidator.Validate(graph);
        if (errors.Count > 0)
        {
            var errorSummary = string.Join("; ", errors);
            logger.LogWarning("Pioneer generated invalid graph: {Errors}", errorSummary);
            return ToolResult.Failure($"Invalid workflow graph: {errorSummary}");
        }

        logger.LogInformation(
            "PioneerPlanTool: executing DAG '{Name}' with {Nodes} nodes",
            graph.Name, graph.Nodes.Count);

        // Step 4: Execute the DAG
        var timeline = new AgentExecutionTimeline();
        var dagResult = await dagExecutor.ExecuteAsync(
            graph,
            new AgentExecutionOptions
            {
                MaxDepth = 5,
                MaxIterations = 10,
                Timeout = TimeSpan.FromMinutes(10)
            },
            context.UserId,
            timeline,
            ct);

        // Step 5: Build a summary of the execution
        var summary = BuildExecutionSummary(graph, dagResult);

        if (dagResult.IsSuccess)
        {
            logger.LogInformation("PioneerPlanTool: workflow '{Name}' completed successfully", graph.Name);
            return ToolResult.Success(summary);
        }

        logger.LogWarning(
            "PioneerPlanTool: workflow '{Name}' failed: {Error}",
            graph.Name, dagResult.ErrorMessage);
        return ToolResult.Failure(summary);
    }

    private static string BuildExecutionSummary(TaskGraph graph, DagExecutionResult dagResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Workflow: {graph.Name}");
        sb.AppendLine($"Status: {(dagResult.IsSuccess ? "Success" : "Failed")}");
        sb.AppendLine($"Total tokens used: {dagResult.TotalTokensUsed}");
        sb.AppendLine();
        sb.AppendLine("Node results:");

        foreach (var node in graph.Nodes)
        {
            sb.Append($"  - {node.Id} ({node.AgentName}): {node.Status}");
            if (!string.IsNullOrEmpty(node.ErrorMessage))
                sb.Append($" - {node.ErrorMessage}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(dagResult.ErrorMessage))
        {
            sb.AppendLine();
            sb.AppendLine($"Error: {dagResult.ErrorMessage}");
        }

        return sb.ToString();
    }
}
