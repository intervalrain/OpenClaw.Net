using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Contracts.Skills;

/// <summary>
/// Parsed agent definition from an AGENT.md file.
/// Structure:
///   agents/{name}/AGENT.md
///   agents/{name}/reference/   (optional - reference docs)
///   agents/{name}/scripts/     (optional - executable scripts)
/// </summary>
public record AgentDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string Version { get; init; } = "1.0";
    public AgentExecutionType ExecutionType { get; init; } = AgentExecutionType.Llm;
    public string? PreferredProvider { get; init; }
    public required string Instructions { get; init; }
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>Directory path of this agent definition (parent of AGENT.md).</summary>
    public string? DirectoryPath { get; init; }

    /// <summary>Reference documents content (from reference/ subdirectory).</summary>
    public IReadOnlyList<SkillResource>? References { get; init; }

    /// <summary>Script file paths (from scripts/ subdirectory).</summary>
    public IReadOnlyList<SkillResource>? Scripts { get; init; }
}
