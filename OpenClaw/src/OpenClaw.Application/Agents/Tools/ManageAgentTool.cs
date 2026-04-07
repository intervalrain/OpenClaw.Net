using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Agents.Entities;
using OpenClaw.Domain.Agents.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Agents.Tools;

/// <summary>
/// Agent tool for creating and managing agent definitions via chat.
/// Supports: create, list, update, delete.
/// </summary>
public class ManageAgentTool(IServiceScopeFactory scopeFactory) : AgentToolBase<ManageAgentArgs>
{
    public override string Name => "manage_agent";
    public override string Description =>
        "Manage agent definitions. Actions: create (new agent), list (show all), update (modify), delete (remove). " +
        "Agents have a name, description, system prompt, list of tool names, and optional sub-agent references. " +
        "Sub-agents are referenced by name and form a DAG (no circular dependencies).";

    public override async Task<ToolResult> ExecuteAsync(ManageAgentArgs args, ToolContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Action))
            return ToolResult.Failure("Action is required (create, list, update, delete).");

        var userId = context.UserId;
        if (userId is null || userId == Guid.Empty)
            return ToolResult.Failure("User context required.");

        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAgentDefinitionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return args.Action.ToLowerInvariant() switch
        {
            "create" => await CreateAsync(args, userId.Value, context.WorkspaceId ?? Guid.Empty, repo, uow, ct),
            "list" => await ListAsync(repo, ct),
            "update" => await UpdateAsync(args, userId.Value, repo, uow, ct),
            "delete" => await DeleteAsync(args, repo, uow, ct),
            _ => ToolResult.Failure($"Unknown action: {args.Action}. Use: create, list, update, delete.")
        };
    }

    private static async Task<ToolResult> CreateAsync(
        ManageAgentArgs args, Guid userId, Guid workspaceId,
        IAgentDefinitionRepository repo, IUnitOfWork uow, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Name))
            return ToolResult.Failure("Name is required for create.");
        if (string.IsNullOrWhiteSpace(args.SystemPrompt))
            return ToolResult.Failure("SystemPrompt is required for create.");

        // Check for duplicate name
        var existing = await repo.GetByNameAsync(args.Name, ct);
        if (existing is not null)
            return ToolResult.Failure($"Agent '{args.Name}' already exists (ID: {existing.Id}). Use update instead.");

        var toolsJson = args.Tools is { Count: > 0 }
            ? JsonSerializer.Serialize(args.Tools)
            : "[]";

        // Resolve sub-agent names to IDs
        var subAgentIdsJson = "[]";
        if (args.SubAgentNames is { Count: > 0 })
        {
            var (ids, error) = await ResolveSubAgentNamesAsync(args.SubAgentNames, repo, ct);
            if (error is not null) return ToolResult.Failure(error);
            subAgentIdsJson = JsonSerializer.Serialize(ids);
        }

        var agent = AgentDefinition.Create(
            userId, workspaceId,
            args.Name,
            args.Description ?? "",
            args.SystemPrompt,
            toolsJson,
            subAgentIdsJson,
            args.MaxIterations ?? 10);

        await repo.AddAsync(agent, ct);
        await uow.SaveChangesAsync(ct);

        return ToolResult.Success(
            $"Created agent '{args.Name}' (ID: {agent.Id}).\n" +
            $"Tools: {toolsJson}\n" +
            $"Invoke with: //{args.Name}");
    }

    private static async Task<ToolResult> ListAsync(IAgentDefinitionRepository repo, CancellationToken ct)
    {
        var agents = await repo.GetAllAsync(ct);

        if (agents.Count == 0)
            return ToolResult.Success("No agents found.");

        var lines = agents.Select(a =>
        {
            var tools = a.ToolsJson != "[]" ? $"\n  Tools: {a.ToolsJson}" : "";
            return $"- {a.Name} (ID: {a.Id})\n  {a.Description}{tools}";
        });

        return ToolResult.Success($"Found {agents.Count} agent(s):\n\n{string.Join("\n\n", lines)}");
    }

    private static async Task<ToolResult> UpdateAsync(
        ManageAgentArgs args, Guid userId,
        IAgentDefinitionRepository repo, IUnitOfWork uow, CancellationToken ct)
    {
        var agent = await FindAgentAsync(args, repo, ct);
        if (agent is null)
            return ToolResult.Failure("Agent not found. Provide agentId or name.");

        var toolsJson = args.Tools is { Count: > 0 }
            ? JsonSerializer.Serialize(args.Tools)
            : null;

        string? subAgentIdsJson = null;
        if (args.SubAgentNames is not null)
        {
            if (args.SubAgentNames.Count == 0)
            {
                subAgentIdsJson = "[]";
            }
            else
            {
                var (ids, error) = await ResolveSubAgentNamesAsync(args.SubAgentNames, repo, ct);
                if (error is not null) return ToolResult.Failure(error);

                // Check circular dependency
                if (HasCircularDependency(agent.Id, ids, await repo.GetAllAsync(ct)))
                    return ToolResult.Failure("Circular dependency detected in sub-agents.");

                subAgentIdsJson = JsonSerializer.Serialize(ids);
            }
        }

        agent.Update(
            name: args.Name,
            description: args.Description,
            systemPrompt: args.SystemPrompt,
            toolsJson: toolsJson,
            subAgentIdsJson: subAgentIdsJson,
            maxIterations: args.MaxIterations);

        await uow.SaveChangesAsync(ct);
        return ToolResult.Success($"Updated agent '{agent.Name}'.");
    }

    private static async Task<ToolResult> DeleteAsync(
        ManageAgentArgs args, IAgentDefinitionRepository repo, IUnitOfWork uow, CancellationToken ct)
    {
        var agent = await FindAgentAsync(args, repo, ct);
        if (agent is null)
            return ToolResult.Failure("Agent not found. Provide agentId or name.");

        // Check if any other agent references this as a sub-agent
        var allAgents = await repo.GetAllAsync(ct);
        var referencedBy = allAgents
            .Where(a => a.Id != agent.Id && a.SubAgentIdsJson.Contains(agent.Id.ToString()))
            .Select(a => a.Name)
            .ToList();

        if (referencedBy.Count > 0)
            return ToolResult.Failure($"Cannot delete: referenced as sub-agent by: {string.Join(", ", referencedBy)}");

        await repo.DeleteAsync(agent, ct);
        await uow.SaveChangesAsync(ct);
        return ToolResult.Success($"Deleted agent '{agent.Name}'.");
    }

    private static async Task<AgentDefinition?> FindAgentAsync(
        ManageAgentArgs args, IAgentDefinitionRepository repo, CancellationToken ct)
    {
        if (args.AgentId.HasValue)
            return await repo.GetByIdAsync(args.AgentId.Value, ct);
        if (!string.IsNullOrWhiteSpace(args.Name))
            return await repo.GetByNameAsync(args.Name, ct);
        return null;
    }

    private static async Task<(List<Guid> ids, string? error)> ResolveSubAgentNamesAsync(
        List<string> names, IAgentDefinitionRepository repo, CancellationToken ct)
    {
        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var sub = await repo.GetByNameAsync(name, ct);
            if (sub is null)
                return ([], $"Sub-agent '{name}' not found. Create it first.");
            ids.Add(sub.Id);
        }
        return (ids, null);
    }

    private static bool HasCircularDependency(Guid agentId, List<Guid> newSubAgentIds, List<AgentDefinition> allAgents)
    {
        var agentMap = allAgents.ToDictionary(a => a.Id);
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>(newSubAgentIds);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == agentId) return true;
            if (!visited.Add(current)) continue;

            if (agentMap.TryGetValue(current, out var agent))
            {
                var childIds = JsonSerializer.Deserialize<List<Guid>>(agent.SubAgentIdsJson) ?? [];
                foreach (var childId in childIds)
                    queue.Enqueue(childId);
            }
        }

        return false;
    }
}

public record ManageAgentArgs(
    [property: Description("Action to perform: create, list, update, delete")]
    string? Action,

    [property: Description("Agent name (used as identifier and for // invocation)")]
    string? Name = null,

    [property: Description("Short description of what this agent does")]
    string? Description = null,

    [property: Description("System prompt / instructions for the agent")]
    string? SystemPrompt = null,

    [property: Description("List of tool names this agent can use (e.g. [\"git\", \"read_file\", \"send_email\"])")]
    List<string>? Tools = null,

    [property: Description("List of sub-agent names this agent can delegate to")]
    List<string>? SubAgentNames = null,

    [property: Description("Maximum LLM iterations (default: 10)")]
    int? MaxIterations = null,

    [property: Description("Agent ID (for update/delete, alternative to name)")]
    Guid? AgentId = null
);
