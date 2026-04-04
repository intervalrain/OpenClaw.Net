using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Loads agent definitions from AGENT.md files within workspace directories.
/// Expected structure per workspace:
///   {workspacePath}/agents/{name}/AGENT.md
///   {workspacePath}/agents/{name}/reference/   (optional)
///   {workspacePath}/agents/{name}/scripts/     (optional)
/// </summary>
public class FileAgentStore(ILogger<FileAgentStore> logger) : IAgentStore
{
    private readonly ConcurrentDictionary<Guid, List<AgentDefinition>> _cache = new();

    public IReadOnlyList<AgentDefinition> GetAllAgents(Guid workspaceId)
        => _cache.GetValueOrDefault(workspaceId) ?? [];

    public AgentDefinition? GetAgent(string name, Guid workspaceId)
        => GetAllAgents(workspaceId)
            .FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public async Task ReloadAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var agentsDirectory = GetWorkspaceAgentsDirectory(workspaceId);
        var newAgents = new List<AgentDefinition>();

        if (!Directory.Exists(agentsDirectory))
        {
            logger.LogDebug("Workspace agents directory not found: {Dir}", agentsDirectory);
            _cache[workspaceId] = newAgents;
            return;
        }

        var files = Directory.GetFiles(agentsDirectory, "AGENT.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var agentDir = Path.GetDirectoryName(file)!;
                var agent = AgentMarkdownParser.Parse(content, file);

                var references = await LoadResourcesAsync(Path.Combine(agentDir, "reference"), ct);
                var scripts = await LoadResourcesAsync(Path.Combine(agentDir, "scripts"), ct);

                var instructions = agent.Instructions;
                if (references.Count > 0)
                {
                    var refContent = string.Join("\n\n", references.Select(r =>
                        $"## Reference: {r.FileName}\n{r.Content}"));
                    instructions += $"\n\n---\n\n{refContent}";
                }

                newAgents.Add(agent with
                {
                    DirectoryPath = agentDir,
                    Instructions = instructions,
                    References = references,
                    Scripts = scripts
                });

                logger.LogInformation(
                    "Loaded agent: {Name} (type={Type}, {RefCount} refs) from {Path}",
                    agent.Name, agent.ExecutionType, references.Count, file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse agent from {Path}", file);
            }
        }

        _cache[workspaceId] = newAgents;
        logger.LogInformation(
            "Loaded {Count} agents for workspace {WorkspaceId} from {Dir}",
            newAgents.Count, workspaceId, agentsDirectory);
    }

    /// <summary>
    /// Resolves the agents directory for a given workspace.
    /// Layout: {workspaceBasePath}/{workspaceId}/agents/
    /// </summary>
    internal static string GetWorkspaceAgentsDirectory(Guid workspaceId)
    {
        var basePath = Environment.GetEnvironmentVariable("OPENCLAW_WORKSPACE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "workspace");
        return Path.Combine(basePath, workspaceId.ToString(), "agents");
    }

    private static async Task<List<SkillResource>> LoadResourcesAsync(string dir, CancellationToken ct)
    {
        var resources = new List<SkillResource>();
        if (!Directory.Exists(dir)) return resources;

        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var relativeName = Path.GetRelativePath(dir, file);
                resources.Add(new SkillResource(relativeName, content));
            }
            catch
            {
                // Skip unreadable files
            }
        }

        return resources;
    }
}
