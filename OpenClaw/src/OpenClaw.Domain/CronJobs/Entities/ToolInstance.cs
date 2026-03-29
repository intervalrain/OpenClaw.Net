using Weda.Core.Domain;

namespace OpenClaw.Domain.CronJobs.Entities;

/// <summary>
/// Represents a pre-configured tool instance with a user-facing name for #reference in prompts.
/// </summary>
public class ToolInstance : Entity<Guid>, IUserScoped
{
    /// <summary>
    /// User-facing name for #reference in prompts.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Actual IAgentTool name that maps to the tool implementation.
    /// </summary>
    public string ToolName { get; private set; } = string.Empty;

    /// <summary>
    /// Pre-filled arguments JSON for the tool.
    /// </summary>
    public string ArgsJson { get; private set; } = string.Empty;

    public string? Description { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ToolInstance() : base(Guid.NewGuid()) { }

    public static ToolInstance Create(
        Guid userId,
        string name,
        string toolName,
        string argsJson,
        string? description = null)
    {
        return new ToolInstance
        {
            Id = Guid.NewGuid(),
            Name = name,
            ToolName = toolName,
            ArgsJson = argsJson,
            Description = description,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string? name = null,
        string? toolName = null,
        string? argsJson = null,
        string? description = null)
    {
        if (name is not null) Name = name;
        if (toolName is not null) ToolName = toolName;
        if (argsJson is not null) ArgsJson = argsJson;
        if (description is not null) Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid GetOwnerUserId() => CreatedByUserId;
}
