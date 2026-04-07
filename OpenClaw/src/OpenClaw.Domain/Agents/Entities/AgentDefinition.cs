using Weda.Core.Domain;

namespace OpenClaw.Domain.Agents.Entities;

/// <summary>
/// A configurable agent that can be created, edited, mounted, and executed.
/// Agents have a system prompt, a set of available tools, and can reference
/// other agents as sub-agents (forming a DAG — no circular dependencies).
/// </summary>
public class AgentDefinition : Entity, IUserScoped
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// System prompt / instructions for this agent.
    /// </summary>
    public string SystemPrompt { get; private set; } = string.Empty;

    /// <summary>
    /// JSON array of tool names this agent can use.
    /// e.g. ["read_file", "execute_shell", "git_status"]
    /// </summary>
    public string ToolsJson { get; private set; } = "[]";

    /// <summary>
    /// JSON array of sub-agent IDs this agent can delegate to.
    /// Forms a DAG — no circular dependencies allowed.
    /// </summary>
    public string SubAgentIdsJson { get; private set; } = "[]";

    public int MaxIterations { get; private set; } = 10;
    public Guid CreatedByUserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AgentDefinition() : base(Guid.NewGuid()) { }

    public Guid GetOwnerUserId() => CreatedByUserId;

    public static AgentDefinition Create(
        Guid userId,
        Guid workspaceId,
        string name,
        string description,
        string systemPrompt,
        string toolsJson = "[]",
        string subAgentIdsJson = "[]",
        int maxIterations = 10)
    {
        return new AgentDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            SystemPrompt = systemPrompt,
            ToolsJson = toolsJson,
            SubAgentIdsJson = subAgentIdsJson,
            MaxIterations = maxIterations,
            CreatedByUserId = userId,
            WorkspaceId = workspaceId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string? name = null,
        string? description = null,
        string? systemPrompt = null,
        string? toolsJson = null,
        string? subAgentIdsJson = null,
        int? maxIterations = null)
    {
        if (name is not null) Name = name;
        if (description is not null) Description = description;
        if (systemPrompt is not null) SystemPrompt = systemPrompt;
        if (toolsJson is not null) ToolsJson = toolsJson;
        if (subAgentIdsJson is not null) SubAgentIdsJson = subAgentIdsJson;
        if (maxIterations.HasValue) MaxIterations = maxIterations.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}