using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

public class DagExecutor(
    IAgentRegistry agentRegistry,
    IServiceProvider serviceProvider,
    ILogger<DagExecutor> logger) : IDagExecutor
{
    public async Task<DagExecutionResult> ExecuteAsync(
        TaskGraph graph,
        AgentExecutionOptions options,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        return await ExecuteAsync(graph, options, userId, timeline: null, ct);
    }

    public async Task<DagExecutionResult> ExecuteAsync(
        TaskGraph graph,
        AgentExecutionOptions options,
        Guid? userId,
        AgentExecutionTimeline? timeline,
        CancellationToken ct = default)
    {
        // Validate
        var errors = TaskGraphValidator.Validate(graph);
        if (errors.Count > 0)
        {
            return new DagExecutionResult
            {
                IsSuccess = false,
                Nodes = graph.Nodes,
                ErrorMessage = string.Join("; ", errors)
            };
        }

        var nodeMap = graph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        // Mark root nodes (no upstream dependencies) as Ready
        foreach (var node in graph.Nodes)
        {
            var upstream = TaskGraphValidator.GetUpstreamNodes(graph, node.Id);
            if (upstream.Count == 0)
                node.Status = TaskNodeStatus.Ready;
        }

        // Execute in waves until all nodes are processed
        while (graph.Nodes.Any(n => n.Status is TaskNodeStatus.Ready))
        {
            ct.ThrowIfCancellationRequested();

            var readyNodes = graph.Nodes.Where(n => n.Status == TaskNodeStatus.Ready).ToList();

            // Execute ready nodes in parallel with a concurrency cap
            var maxParallelism = Math.Max(1, options.MaxParallelism);
            using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
            var tasks = readyNodes.Select(async node =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    await ExecuteNodeAsync(node, graph, nodeMap, options, userId, timeline, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            });
            await Task.WhenAll(tasks);

            // After execution, check downstream nodes for readiness
            foreach (var completedNode in readyNodes.Where(n => n.Status == TaskNodeStatus.Completed))
            {
                var downstream = TaskGraphValidator.GetDownstreamNodes(graph, completedNode.Id);
                foreach (var downId in downstream)
                {
                    var downNode = nodeMap[downId];
                    if (downNode.Status != TaskNodeStatus.Pending)
                        continue;

                    // Check if ALL upstream nodes are completed
                    var upstreamIds = TaskGraphValidator.GetUpstreamNodes(graph, downId);
                    if (upstreamIds.All(uid => nodeMap[uid].Status == TaskNodeStatus.Completed))
                    {
                        // Map outputs from upstream to this node's input
                        MapOutputsToInput(graph, downNode, nodeMap);
                        downNode.Status = TaskNodeStatus.Ready;
                    }
                }
            }

            // Mark downstream of failed nodes as Skipped
            foreach (var failedNode in readyNodes.Where(n => n.Status == TaskNodeStatus.Failed))
            {
                SkipDownstream(graph, failedNode.Id, nodeMap, timeline);
            }
        }

        var allCompleted = graph.Nodes.All(n => n.Status == TaskNodeStatus.Completed);
        var totalTokens = graph.Nodes.Sum(n => n.TokensUsed);

        return new DagExecutionResult
        {
            IsSuccess = allCompleted,
            Nodes = graph.Nodes,
            ErrorMessage = allCompleted ? null : "Some nodes failed or were skipped.",
            TotalTokensUsed = totalTokens
        };
    }

    private async Task ExecuteNodeAsync(
        TaskNode node,
        TaskGraph graph,
        Dictionary<string, TaskNode> nodeMap,
        AgentExecutionOptions options,
        Guid? userId,
        AgentExecutionTimeline? timeline,
        CancellationToken ct)
    {
        node.Status = TaskNodeStatus.Running;
        timeline?.Record(node.AgentName, AgentTimelineEventType.Started, $"Node '{node.Id}'");

        var agent = agentRegistry.GetAgent(node.AgentName);
        if (agent is null)
        {
            node.Status = TaskNodeStatus.Failed;
            node.ErrorMessage = $"Agent '{node.AgentName}' not found in registry.";
            timeline?.Record(node.AgentName, AgentTimelineEventType.Failed, node.ErrorMessage);
            logger.LogWarning("DAG node '{NodeId}': agent '{AgentName}' not found", node.Id, node.AgentName);
            return;
        }

        try
        {
            var context = new AgentExecutionContext
            {
                Input = node.Input ?? JsonDocument.Parse("{}"),
                Services = serviceProvider,
                Options = options,
                UserId = userId
            };

            var result = await agent.ExecuteAsync(context, ct);

            if (result.Status == AgentResultStatus.Success)
            {
                node.Status = TaskNodeStatus.Completed;
                node.Output = result.Output;
                node.TokensUsed = result.TokensUsed;
                timeline?.Record(node.AgentName, AgentTimelineEventType.Completed,
                    $"Node '{node.Id}', tokens={result.TokensUsed}");
                logger.LogInformation("DAG node '{NodeId}' completed successfully", node.Id);
            }
            else
            {
                node.Status = TaskNodeStatus.Failed;
                node.ErrorMessage = result.ErrorMessage;
                timeline?.Record(node.AgentName, AgentTimelineEventType.Failed, result.ErrorMessage);
                logger.LogWarning("DAG node '{NodeId}' failed: {Error}", node.Id, result.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            node.Status = TaskNodeStatus.Failed;
            node.ErrorMessage = "Execution was cancelled.";
            timeline?.Record(node.AgentName, AgentTimelineEventType.Failed, "Cancelled");
        }
        catch (Exception ex)
        {
            node.Status = TaskNodeStatus.Failed;
            node.ErrorMessage = ex.Message;
            timeline?.Record(node.AgentName, AgentTimelineEventType.Failed, ex.Message);
            logger.LogError(ex, "DAG node '{NodeId}' threw an exception", node.Id);
        }
    }

    /// <summary>
    /// Maps outputs from upstream completed nodes to a downstream node's input via edge OutputMapping.
    /// </summary>
    private static void MapOutputsToInput(
        TaskGraph graph,
        TaskNode targetNode,
        Dictionary<string, TaskNode> nodeMap)
    {
        var incomingEdges = graph.Edges
            .Where(e => string.Equals(e.ToNodeId, targetNode.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (incomingEdges.Count == 0)
            return;

        var merged = new Dictionary<string, JsonElement>();

        // Preserve any static input the node already has
        if (targetNode.Input is not null)
        {
            foreach (var prop in targetNode.Input.RootElement.EnumerateObject())
            {
                merged[prop.Name] = prop.Value.Clone();
            }
        }

        foreach (var edge in incomingEdges)
        {
            var sourceNode = nodeMap[edge.FromNodeId];
            if (sourceNode.Output is null)
                continue;

            if (edge.OutputMapping is not null)
            {
                // Simple JSONPath: support "$.prop.subprop" style navigation
                var value = ResolveJsonPath(sourceNode.Output, edge.OutputMapping);
                if (value.HasValue)
                {
                    // Use the last segment as the key name
                    var key = GetLastPathSegment(edge.OutputMapping);
                    merged[key] = value.Value.Clone();
                }
            }
            else
            {
                // No mapping — merge entire output from source
                foreach (var prop in sourceNode.Output.RootElement.EnumerateObject())
                {
                    merged[prop.Name] = prop.Value.Clone();
                }
            }
        }

        targetNode.Input = JsonDocument.Parse(JsonSerializer.Serialize(merged));
    }

    /// <summary>
    /// Simple JSONPath resolver supporting "$.prop.subprop" dot notation.
    /// </summary>
    internal static JsonElement? ResolveJsonPath(JsonDocument doc, string path)
    {
        var segments = path.TrimStart('$').Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonElement current = doc.RootElement;

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(segment, out var next))
                return null;

            current = next;
        }

        return current;
    }

    private static string GetLastPathSegment(string path)
    {
        var segments = path.TrimStart('$').Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : "value";
    }

    private static void SkipDownstream(
        TaskGraph graph,
        string failedNodeId,
        Dictionary<string, TaskNode> nodeMap,
        AgentExecutionTimeline? timeline = null)
    {
        var toSkip = new Queue<string>();
        foreach (var downId in TaskGraphValidator.GetDownstreamNodes(graph, failedNodeId))
            toSkip.Enqueue(downId);

        while (toSkip.Count > 0)
        {
            var id = toSkip.Dequeue();
            var node = nodeMap[id];
            if (node.Status is TaskNodeStatus.Pending or TaskNodeStatus.Ready)
            {
                node.Status = TaskNodeStatus.Skipped;
                node.ErrorMessage = $"Skipped due to upstream failure of '{failedNodeId}'.";
                timeline?.Record(node.AgentName, AgentTimelineEventType.Skipped, node.ErrorMessage);

                foreach (var furtherDown in TaskGraphValidator.GetDownstreamNodes(graph, id))
                    toSkip.Enqueue(furtherDown);
            }
        }
    }
}
