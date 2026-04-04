using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.HierarchicalAgents.Controllers;

[ApiVersion("1.0")]
public class AgentExecutionsController(
    IAgentRegistry agentRegistry,
    IDagExecutor dagExecutor,
    ICurrentUserProvider currentUserProvider,
    IServiceProvider serviceProvider) : ApiController
{
    /// <summary>
    /// Executes a single agent by name with the given input.
    /// </summary>
    [HttpPost("run/{agentName}")]
    public async Task<IActionResult> RunAgent(
        string agentName,
        [FromBody] JsonDocument input,
        CancellationToken ct)
    {
        var agent = agentRegistry.GetAgent(agentName);
        if (agent is null)
            return NotFound(new { error = $"Agent '{agentName}' not found." });

        var context = new AgentExecutionContext
        {
            Input = input,
            Services = serviceProvider,
            Options = new AgentExecutionOptions { BudgetLimit = 100_000 },
            UserId = GetUserId()
        };

        var result = await agent.ExecuteAsync(context, ct);

        return Ok(new
        {
            Agent = agentName,
            Status = result.Status.ToString(),
            result.Output,
            result.TokensUsed,
            result.ErrorMessage,
            Timeline = context.Timeline.GetEvents().Select(e => new
            {
                e.Timestamp,
                e.AgentName,
                Type = e.Type.ToString(),
                e.Detail
            })
        });
    }

    /// <summary>
    /// Executes a DAG workflow from a JSON graph definition.
    /// </summary>
    [HttpPost("run-dag")]
    public async Task<IActionResult> RunDag(
        [FromBody] JsonDocument graphJson,
        CancellationToken ct)
    {
        TaskGraph? graph;
        try
        {
            graph = TaskGraphSerializer.DeserializeJson(graphJson.RootElement.GetRawText());
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Invalid graph JSON: {ex.Message}" });
        }

        if (graph is null)
            return BadRequest(new { error = "Failed to parse graph." });

        var errors = TaskGraphValidator.Validate(graph);
        if (errors.Count > 0)
            return BadRequest(new { errors });

        var timeline = new AgentExecutionTimeline();
        var userId = GetUserId();
        var result = await dagExecutor.ExecuteAsync(graph, new AgentExecutionOptions { BudgetLimit = 100_000 }, userId, timeline, ct);

        return Ok(new
        {
            Workflow = graph.Name,
            result.IsSuccess,
            result.TotalTokensUsed,
            result.ErrorMessage,
            Nodes = result.Nodes.Select(n => new
            {
                n.Id,
                Agent = n.AgentName,
                Status = n.Status.ToString(),
                n.TokensUsed,
                n.ErrorMessage
            }),
            Timeline = timeline.GetEvents().Select(e => new
            {
                e.Timestamp,
                e.AgentName,
                Type = e.Type.ToString(),
                e.Detail
            })
        });
    }

    private Guid? GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return null; }
    }
}
