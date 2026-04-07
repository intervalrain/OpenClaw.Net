using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Workspaces;
using OpenClaw.Domain.Agents.Entities;
using OpenClaw.Domain.Agents.Repositories;
using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Agents.Controllers;

[ApiVersion("1.0")]
public class AgentsController(
    IToolRegistry toolRegistry,
    IAgentDefinitionRepository agentRepo,
    ICurrentUserProvider currentUserProvider,
    ICurrentWorkspaceProvider workspaceProvider,
    IUnitOfWork uow) : ApiController
{
    // ── Tools (read-only, for reference) ──

    [HttpGet("tools")]
    public IActionResult GetTools()
    {
        var tools = toolRegistry.GetAllSkills().Select(t => new AgentToolDto
        {
            Name = t.Name,
            Description = t.Description,
            PermissionLevel = t.PermissionLevel.ToString(),
            IsStreaming = t is IStreamingAgentTool,
            Parameters = t.Parameters
        }).ToList();

        return Ok(tools);
    }

    // ── Agent CRUD ──

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var agents = await agentRepo.GetAllAsync(ct);
        return Ok(agents.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var agent = await agentRepo.GetByIdAsync(id, ct);
        return agent is null ? NotFound() : Ok(ToDto(agent));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAgentRequest req, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();

        // Validate sub-agent references (no circular deps)
        if (req.SubAgentIds is { Count: > 0 })
        {
            var existing = await agentRepo.GetAllAsync(ct);
            var existingIds = existing.Select(a => a.Id).ToHashSet();
            foreach (var subId in req.SubAgentIds)
            {
                if (!existingIds.Contains(subId))
                    return BadRequest($"Sub-agent '{subId}' not found");
            }
        }

        var agent = AgentDefinition.Create(
            user.Id,
            workspaceProvider.WorkspaceId,
            req.Name,
            req.Description ?? "",
            req.SystemPrompt ?? "",
            JsonSerializer.Serialize(req.Tools ?? []),
            JsonSerializer.Serialize(req.SubAgentIds ?? []),
            req.MaxIterations ?? 10);

        await agentRepo.AddAsync(agent, ct);
        await uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, ToDto(agent));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentRequest req, CancellationToken ct)
    {
        var agent = await agentRepo.GetByIdAsync(id, ct);
        if (agent is null) return NotFound();

        // Validate circular dependency if sub-agents changed
        if (req.SubAgentIds is not null)
        {
            if (req.SubAgentIds.Contains(id))
                return BadRequest("Agent cannot reference itself as sub-agent");

            var allAgents = await agentRepo.GetAllAsync(ct);
            if (HasCircularDependency(id, req.SubAgentIds, allAgents))
                return BadRequest("Circular dependency detected in sub-agent references");
        }

        agent.Update(
            name: req.Name,
            description: req.Description,
            systemPrompt: req.SystemPrompt,
            toolsJson: req.Tools is not null ? JsonSerializer.Serialize(req.Tools) : null,
            subAgentIdsJson: req.SubAgentIds is not null ? JsonSerializer.Serialize(req.SubAgentIds) : null,
            maxIterations: req.MaxIterations);

        await agentRepo.UpdateAsync(agent, ct);
        await uow.SaveChangesAsync(ct);

        return Ok(ToDto(agent));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var agent = await agentRepo.GetByIdAsync(id, ct);
        if (agent is null) return NotFound();

        // Check if any other agent references this one as sub-agent
        var allAgents = await agentRepo.GetAllAsync(ct);
        var referencedBy = allAgents
            .Where(a => a.Id != id && ParseGuids(a.SubAgentIdsJson).Contains(id))
            .Select(a => a.Name)
            .ToList();

        if (referencedBy.Count > 0)
            return BadRequest($"Cannot delete: referenced as sub-agent by {string.Join(", ", referencedBy)}");

        await agentRepo.DeleteAsync(agent, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── DAG ──

    [HttpGet("dag")]
    public async Task<IActionResult> GetDag(CancellationToken ct)
    {
        var agents = await agentRepo.GetAllAsync(ct);

        var nodes = new List<DagNodeDto>();
        var edges = new List<DagEdgeDto>();

        foreach (var agent in agents)
        {
            nodes.Add(new DagNodeDto
            {
                Id = agent.Id.ToString(),
                Label = agent.Name,
                Type = "agent",
                Description = agent.Description
            });

            foreach (var subId in ParseGuids(agent.SubAgentIdsJson))
            {
                edges.Add(new DagEdgeDto
                {
                    From = agent.Id.ToString(),
                    To = subId.ToString(),
                    Label = "sub-agent"
                });
            }
        }

        return Ok(new DagDto { Nodes = nodes, Edges = edges });
    }

    [HttpGet("{id:guid}/dag")]
    public async Task<IActionResult> GetAgentDag(Guid id, CancellationToken ct)
    {
        var allAgents = await agentRepo.GetAllAsync(ct);
        var agentMap = allAgents.ToDictionary(a => a.Id);

        if (!agentMap.ContainsKey(id))
            return NotFound();

        // BFS to collect reachable agents
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(id);

        var nodes = new List<DagNodeDto>();
        var edges = new List<DagEdgeDto>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current) || !agentMap.TryGetValue(current, out var agent))
                continue;

            nodes.Add(new DagNodeDto
            {
                Id = agent.Id.ToString(),
                Label = agent.Name,
                Type = current == id ? "root" : "agent",
                Description = agent.Description
            });

            foreach (var subId in ParseGuids(agent.SubAgentIdsJson))
            {
                edges.Add(new DagEdgeDto
                {
                    From = agent.Id.ToString(),
                    To = subId.ToString(),
                    Label = "sub-agent"
                });
                queue.Enqueue(subId);
            }
        }

        return Ok(new DagDto { Nodes = nodes, Edges = edges });
    }

    // ── Helpers ──

    private static bool HasCircularDependency(Guid agentId, List<Guid> newSubAgentIds, List<AgentDefinition> allAgents)
    {
        var agentMap = allAgents.ToDictionary(a => a.Id);
        var visited = new HashSet<Guid>();
        var stack = new Queue<Guid>(newSubAgentIds);

        while (stack.Count > 0)
        {
            var current = stack.Dequeue();
            if (current == agentId) return true;
            if (!visited.Add(current)) continue;

            if (agentMap.TryGetValue(current, out var agent))
            {
                foreach (var subId in ParseGuids(agent.SubAgentIdsJson))
                    stack.Enqueue(subId);
            }
        }

        return false;
    }

    private static List<Guid> ParseGuids(string json)
    {
        try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? []; }
        catch { return []; }
    }

    private static AgentDto ToDto(AgentDefinition a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Description = a.Description,
        SystemPrompt = a.SystemPrompt,
        Tools = JsonSerializer.Deserialize<List<string>>(a.ToolsJson) ?? [],
        SubAgentIds = ParseGuids(a.SubAgentIdsJson),
        MaxIterations = a.MaxIterations,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };
}

// ── DTOs ──

public class AgentDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string SystemPrompt { get; init; }
    public required List<string> Tools { get; init; }
    public required List<Guid> SubAgentIds { get; init; }
    public int MaxIterations { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public class CreateAgentRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? SystemPrompt { get; init; }
    public List<string>? Tools { get; init; }
    public List<Guid>? SubAgentIds { get; init; }
    public int? MaxIterations { get; init; }
}

public class UpdateAgentRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? SystemPrompt { get; init; }
    public List<string>? Tools { get; init; }
    public List<Guid>? SubAgentIds { get; init; }
    public int? MaxIterations { get; init; }
}

public class AgentToolDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PermissionLevel { get; init; }
    public bool IsStreaming { get; init; }
    public object? Parameters { get; init; }
}

public class DagDto
{
    public required List<DagNodeDto> Nodes { get; init; }
    public required List<DagEdgeDto> Edges { get; init; }
}

public class DagNodeDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Type { get; init; } // "agent", "root"
    public string? Description { get; init; }
}

public class DagEdgeDto
{
    public required string From { get; init; }
    public required string To { get; init; }
    public string? Label { get; init; }
}
