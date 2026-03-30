using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Loads agent definitions from AGENT.md files in a directory.
/// Expected structure:
///   agents/{name}/AGENT.md
///   agents/{name}/reference/   (optional - reference docs)
///   agents/{name}/scripts/     (optional - scripts)
/// </summary>
public class FileAgentStore(string agentsDirectory, ILogger<FileAgentStore> logger) : IAgentStore
{
    private List<AgentDefinition> _agents = [];

    public IReadOnlyList<AgentDefinition> GetAllAgents() => _agents;

    public AgentDefinition? GetAgent(string name)
        => _agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var newAgents = new List<AgentDefinition>();

        if (!Directory.Exists(agentsDirectory))
        {
            logger.LogWarning("Agents directory not found: {Dir}", agentsDirectory);
            _agents = newAgents;
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

        _agents = newAgents;
        logger.LogInformation("Loaded {Count} agents from {Dir}", newAgents.Count, agentsDirectory);
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
