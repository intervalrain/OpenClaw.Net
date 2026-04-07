using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Skills;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Agents.Controllers;

[ApiVersion("1.0")]
public class AgentsController(
    IToolRegistry toolRegistry,
    ISkillStore skillStore) : ApiController
{
    /// <summary>
    /// Lists all registered tools with metadata.
    /// </summary>
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

    /// <summary>
    /// Lists all registered skills (SKILL.md).
    /// </summary>
    [HttpGet("skills")]
    public IActionResult GetSkills()
    {
        var skills = skillStore.GetAllSkills().Select(s => new AgentSkillDto
        {
            Name = s.Name,
            Description = s.Description,
            Tools = s.Tools.ToList()
        }).ToList();

        return Ok(skills);
    }

    /// <summary>
    /// Returns the DAG structure showing skill → tool dependencies.
    /// </summary>
    [HttpGet("dag")]
    public IActionResult GetDag()
    {
        var allTools = toolRegistry.GetAllSkills();
        var allSkills = skillStore.GetAllSkills();
        var toolNames = new HashSet<string>(allTools.Select(t => t.Name));

        var nodes = new List<DagNodeDto>();
        var edges = new List<DagEdgeDto>();

        // Add tool nodes
        foreach (var tool in allTools)
        {
            nodes.Add(new DagNodeDto
            {
                Id = $"tool:{tool.Name}",
                Label = tool.Name,
                Type = "tool",
                Description = tool.Description,
                PermissionLevel = tool.PermissionLevel.ToString()
            });
        }

        // Add skill nodes and edges (skill → tools it uses)
        foreach (var skill in allSkills)
        {
            var skillId = $"skill:{skill.Name}";
            nodes.Add(new DagNodeDto
            {
                Id = skillId,
                Label = $"@{skill.Name}",
                Type = "skill",
                Description = skill.Description
            });

            foreach (var toolName in skill.Tools)
            {
                if (toolNames.Contains(toolName))
                {
                    edges.Add(new DagEdgeDto
                    {
                        From = skillId,
                        To = $"tool:{toolName}",
                        Label = "uses"
                    });
                }
            }
        }

        // Add pipeline node
        nodes.Add(new DagNodeDto
        {
            Id = "pipeline",
            Label = "AgentPipeline",
            Type = "pipeline",
            Description = "Main agent execution pipeline"
        });

        // Pipeline → special tools
        var specialTools = new[] { "spawn_agent", "tool_search", "enter_plan_mode", "exit_plan_mode", "validate_json" };
        foreach (var name in specialTools)
        {
            if (toolNames.Contains(name))
            {
                edges.Add(new DagEdgeDto { From = "pipeline", To = $"tool:{name}", Label = "built-in" });
            }
        }

        // spawn_agent → pipeline (recursive)
        if (toolNames.Contains("spawn_agent"))
        {
            edges.Add(new DagEdgeDto { From = "tool:spawn_agent", To = "pipeline", Label = "spawns" });
        }

        return Ok(new DagDto { Nodes = nodes, Edges = edges });
    }
}

// --- DTOs ---

public class AgentToolDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PermissionLevel { get; init; }
    public bool IsStreaming { get; init; }
    public object? Parameters { get; init; }
}

public class AgentSkillDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required List<string> Tools { get; init; }
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
    public required string Type { get; init; } // "tool", "skill", "pipeline"
    public string? Description { get; init; }
    public string? PermissionLevel { get; init; }
}

public class DagEdgeDto
{
    public required string From { get; init; }
    public required string To { get; init; }
    public string? Label { get; init; }
}