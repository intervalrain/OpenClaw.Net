using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Creates new agents in a workspace by writing AGENT.md and associated files.
/// </summary>
public interface IPioneerCreateService
{
    Task<CreateAgentResult> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);
}

public record CreateAgentRequest
{
    public required Guid WorkspaceId { get; init; }
    public required string AgentName { get; init; }
    public required string AgentMdContent { get; init; }
    public Dictionary<string, string> Scripts { get; init; } = new();
    public string? WorkflowYaml { get; init; }
}

public record CreateAgentResult(bool IsSuccess, string? AgentPath, string? ErrorMessage);

public partial class PioneerCreateService(ILogger<PioneerCreateService> logger) : IPioneerCreateService
{
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex AgentNamePattern();

    public async Task<CreateAgentResult> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        // 1. Validate agent name
        if (string.IsNullOrWhiteSpace(request.AgentName))
            return new CreateAgentResult(false, null, "Agent name is required.");

        if (!AgentNamePattern().IsMatch(request.AgentName))
            return new CreateAgentResult(false, null,
                "Agent name must be lowercase-kebab-case (alphanumeric and hyphens only, e.g. 'my-agent').");

        if (string.IsNullOrWhiteSpace(request.AgentMdContent))
            return new CreateAgentResult(false, null, "AGENT.md content is required.");

        // 2. Get workspace agents directory (same resolution as FileAgentStore)
        var agentsDir = FileAgentStore.GetWorkspaceAgentsDirectory(request.WorkspaceId);
        var agentDir = Path.Combine(agentsDir, request.AgentName);

        try
        {
            // 3. Create agent directory
            Directory.CreateDirectory(agentDir);

            // 4. Write AGENT.md
            var agentMdPath = Path.Combine(agentDir, "AGENT.md");
            await File.WriteAllTextAsync(agentMdPath, request.AgentMdContent, ct);

            // 5. Write scripts
            if (request.Scripts.Count > 0)
            {
                var scriptsDir = Path.Combine(agentDir, "scripts");
                Directory.CreateDirectory(scriptsDir);

                foreach (var (filename, content) in request.Scripts)
                {
                    var scriptPath = Path.Combine(scriptsDir, filename);

                    // Validate script filename (no path traversal)
                    var resolvedPath = Path.GetFullPath(scriptPath);
                    if (!resolvedPath.StartsWith(Path.GetFullPath(scriptsDir), StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning("Skipping script with path traversal attempt: {Filename}", filename);
                        continue;
                    }

                    await File.WriteAllTextAsync(scriptPath, content, ct);
                }
            }

            // 6. Write workflow.yaml if provided
            if (!string.IsNullOrWhiteSpace(request.WorkflowYaml))
            {
                var workflowPath = Path.Combine(agentDir, "workflow.yaml");
                await File.WriteAllTextAsync(workflowPath, request.WorkflowYaml, ct);
            }

            // 7. Validate the created AGENT.md is parseable
            try
            {
                AgentMarkdownParser.Parse(request.AgentMdContent, agentMdPath);
            }
            catch (FormatException ex)
            {
                // Clean up on validation failure
                Directory.Delete(agentDir, recursive: true);
                return new CreateAgentResult(false, null, $"AGENT.md validation failed: {ex.Message}");
            }

            logger.LogInformation("Created agent '{AgentName}' at {AgentDir}", request.AgentName, agentDir);

            // 8. Return the agent path
            return new CreateAgentResult(true, agentDir, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create agent '{AgentName}'", request.AgentName);
            return new CreateAgentResult(false, null, $"Failed to create agent: {ex.Message}");
        }
    }
}
