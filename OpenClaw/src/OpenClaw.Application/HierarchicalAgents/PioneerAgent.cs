using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Pioneer Agent: uses a strong LLM to decompose user tasks into executable DAGs.
/// Receives a user task, queries available agents from the registry,
/// and outputs a validated TaskGraph.
/// </summary>
public class PioneerAgent : AgentBase
{
    public override string Name => "pioneer";
    public override string Description => "Decomposes user tasks into executable DAG workflows using a strong LLM.";
    public override string Version => "1.0";
    public override AgentExecutionType ExecutionType => AgentExecutionType.Llm;
    public override string? PreferredProvider => null; // Uses default (strongest available)

    protected override async Task<AgentResult> ExecuteCoreAsync(
        AgentExecutionContext context, CancellationToken ct)
    {
        var logger = context.Services.GetRequiredService<ILogger<PioneerAgent>>();
        var providerFactory = context.Services.GetRequiredService<ILlmProviderFactory>();
        var agentRegistry = context.Services.GetRequiredService<IAgentRegistry>();

        var provider = context.UserId.HasValue
            ? await providerFactory.GetProviderAsync(context.UserId.Value, PreferredProvider, ct)
            : await providerFactory.GetProviderAsync(ct);

        // Build agent registry dump for the prompt
        var agentDump = BuildAgentRegistryDump(agentRegistry);

        var systemPrompt = BuildSystemPrompt(agentDump);
        systemPrompt = await PreferenceInjector.EnrichWithPreferencesAsync(systemPrompt, context, ct);
        var userTask = context.Input.RootElement.TryGetProperty("task", out var taskProp)
            ? taskProp.GetString() ?? context.Input.RootElement.ToString()
            : context.Input.RootElement.ToString();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userTask)
        };

        logger.LogInformation("Pioneer planning for task: {Task}", userTask[..Math.Min(100, userTask.Length)]);

        var response = await provider.ChatAsync(messages, ct: ct);

        if (string.IsNullOrWhiteSpace(response.Content))
            return AgentResult.Failed("Pioneer received empty response from LLM.");

        // Parse the LLM response as a TaskGraph JSON
        var graph = TryParseTaskGraph(response.Content, logger);
        if (graph is null)
            return AgentResult.Failed("Pioneer failed to parse LLM response as a valid TaskGraph.");

        // Validate the generated graph
        var errors = TaskGraphValidator.Validate(graph);
        if (errors.Count > 0)
        {
            logger.LogWarning("Pioneer generated invalid graph: {Errors}", string.Join("; ", errors));
            return AgentResult.Failed($"Pioneer generated invalid graph: {string.Join("; ", errors)}");
        }

        // Validate all referenced agents exist
        var missingAgents = graph.Nodes
            .Where(n => agentRegistry.GetAgent(n.AgentName) is null)
            .Select(n => n.AgentName)
            .Distinct()
            .ToList();

        if (missingAgents.Count > 0)
        {
            logger.LogWarning("Pioneer referenced unknown agents: {Agents}", string.Join(", ", missingAgents));
            return AgentResult.Failed($"Pioneer referenced unknown agents: {string.Join(", ", missingAgents)}");
        }

        logger.LogInformation("Pioneer generated DAG '{Name}' with {NodeCount} nodes",
            graph.Name, graph.Nodes.Count);

        var serialized = TaskGraphSerializer.SerializeJson(graph);
        var output = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            graph = JsonSerializer.Deserialize<JsonElement>(serialized),
            nodeCount = graph.Nodes.Count,
            edgeCount = graph.Edges.Count
        }));

        return AgentResult.Success(output);
    }

    private static string BuildAgentRegistryDump(IAgentRegistry registry)
    {
        var agents = registry.GetAll();
        if (agents.Count == 0)
            return "No agents available.";

        var lines = agents.Select(a =>
        {
            var schema = a.InputSchema is not null
                ? a.InputSchema.RootElement.ToString()
                : "{}";
            return $"- **{a.Name}** ({a.ExecutionType}): {a.Description}\n  Input: {schema}";
        });

        return string.Join("\n", lines);
    }

    internal static string BuildSystemPrompt(string agentRegistryDump)
    {
        return $$"""
            You are a task planner (Pioneer Agent). Given a user request, decompose it into a DAG of agent calls.

            ## Available Agents
            {{agentRegistryDump}}

            ## Output Format
            Respond with ONLY a JSON object (no markdown fences, no explanation) in this exact format:
            {
              "name": "descriptive-workflow-name",
              "nodes": [
                {
                  "id": "unique-node-id",
                  "agent": "agent-name-from-list-above",
                  "input": { "key": "value" }
                }
              ],
              "edges": [
                {
                  "from": "source-node-id",
                  "to": "target-node-id",
                  "mapping": "$.output.propertyName"
                }
              ]
            }

            ## Rules
            1. Use the MINIMUM number of agents needed to accomplish the task
            2. Prefer deterministic agents over LLM agents when possible
            3. Ensure all required inputs are mapped via edges or provided as static input
            4. Node IDs must be unique, descriptive, and lowercase-kebab-case
            5. Only reference agents that exist in the Available Agents list above
            6. If the task can be done by a single agent, create a single-node graph
            7. Edges define data flow: "mapping" uses JSONPath to extract upstream output
            8. If no mapping is needed, omit the "mapping" field
            """;
    }

    private static TaskGraph? TryParseTaskGraph(string content, ILogger logger)
    {
        // Try to extract JSON from the response (handle markdown fences)
        var json = ExtractJson(content);
        if (json is null)
        {
            logger.LogWarning("Could not extract JSON from Pioneer response");
            return null;
        }

        try
        {
            return TaskGraphSerializer.DeserializeJson(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize Pioneer response as TaskGraph");
            return null;
        }
    }

    /// <summary>
    /// Extracts JSON from LLM response, handling markdown code fences.
    /// </summary>
    internal static string? ExtractJson(string content)
    {
        content = content.Trim();

        // Try direct parse first
        if (content.StartsWith('{'))
            return content;

        // Handle ```json ... ``` or ``` ... ```
        var fenceStart = content.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart < 0) return null;

        var contentAfterFence = content[(fenceStart + 3)..];
        // Skip optional language identifier (e.g., "json")
        var newline = contentAfterFence.IndexOf('\n');
        if (newline >= 0)
            contentAfterFence = contentAfterFence[(newline + 1)..];

        var fenceEnd = contentAfterFence.IndexOf("```", StringComparison.Ordinal);
        if (fenceEnd < 0) return contentAfterFence.Trim();

        return contentAfterFence[..fenceEnd].Trim();
    }
}
