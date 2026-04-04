using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Workspaces;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.HierarchicalAgents.Controllers;

[ApiVersion("1.0")]
public class AgentsController(
    IAgentRegistry agentRegistry,
    IAgentStore agentStore,
    ICurrentWorkspaceProvider currentWorkspaceProvider) : ApiController
{
    private Guid WorkspaceId => currentWorkspaceProvider.WorkspaceId;

    /// <summary>
    /// Lists all agents: built-in + workspace-scoped.
    /// </summary>
    [HttpGet]
    public IActionResult ListAgents()
    {
        var agents = agentRegistry.GetAll(WorkspaceId)
            .Select(a => new
            {
                a.Name,
                a.Description,
                a.Version,
                ExecutionType = a.ExecutionType.ToString(),
                a.PreferredProvider
            })
            .OrderBy(a => a.Name)
            .ToList();

        return Ok(agents);
    }

    /// <summary>
    /// Gets a specific agent by name (checks workspace agents first).
    /// </summary>
    [HttpGet("{name}")]
    public IActionResult GetAgent(string name)
    {
        var agent = agentRegistry.GetAgent(name, WorkspaceId);
        if (agent is null)
            return NotFound();

        return Ok(new
        {
            agent.Name,
            agent.Description,
            agent.Version,
            ExecutionType = agent.ExecutionType.ToString(),
            agent.PreferredProvider,
            HasInputSchema = agent.InputSchema is not null,
            HasOutputSchema = agent.OutputSchema is not null
        });
    }

    /// <summary>
    /// Lists all file-defined agent definitions (AGENT.md) for the current workspace.
    /// </summary>
    [HttpGet("definitions")]
    public IActionResult ListDefinitions()
    {
        var definitions = agentStore.GetAllAgents(WorkspaceId)
            .Select(d => new
            {
                d.Name,
                d.Description,
                d.Version,
                ExecutionType = d.ExecutionType.ToString(),
                d.PreferredProvider,
                ToolCount = d.Tools.Count,
                d.Tools
            })
            .OrderBy(d => d.Name)
            .ToList();

        return Ok(definitions);
    }

    /// <summary>
    /// Reloads agent definitions from disk for the current workspace.
    /// </summary>
    [HttpPost("definitions/reload")]
    [Authorize(Policy = Policy.AdminOrAbove)]
    public async Task<IActionResult> ReloadDefinitions(CancellationToken ct)
    {
        await agentStore.ReloadAsync(WorkspaceId, ct);

        return Ok(new
        {
            message = "Agent definitions reloaded.",
            count = agentStore.GetAllAgents(WorkspaceId).Count
        });
    }
}
