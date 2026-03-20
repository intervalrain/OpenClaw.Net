using Weda.Core.Domain;

namespace OpenClaw.Domain.Workflows.Entities;

/// <summary>
/// Represents a workflow definition that can be executed.
/// </summary>
public class WorkflowDefinition : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>
    /// Serialized WorkflowGraph JSON containing nodes and edges.
    /// </summary>
    public string DefinitionJson { get; private set; } = string.Empty;

    /// <summary>
    /// Serialized ScheduleConfig JSON, if scheduling is enabled.
    /// </summary>
    public string? ScheduleJson { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Last time this workflow was executed by the scheduler.
    /// Used for scheduling logic to avoid duplicate runs.
    /// </summary>
    public DateTime? LastScheduledAt { get; private set; }

    // Navigation property
    private readonly List<WorkflowExecution> _executions = [];
    public IReadOnlyCollection<WorkflowExecution> Executions => _executions.AsReadOnly();

    private WorkflowDefinition() : base(Guid.NewGuid()) { }

    public static WorkflowDefinition Create(
        Guid userId,
        string name,
        string? description,
        string definitionJson,
        string? scheduleJson = null)
    {
        return new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DefinitionJson = definitionJson,
            ScheduleJson = scheduleJson,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Update(
        string? name = null,
        string? description = null,
        string? definitionJson = null,
        string? scheduleJson = null,
        bool? isActive = null)
    {
        if (name is not null) Name = name;
        if (description is not null) Description = description;
        if (definitionJson is not null) DefinitionJson = definitionJson;
        if (scheduleJson is not null) ScheduleJson = scheduleJson;
        if (isActive.HasValue) IsActive = isActive.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearSchedule()
    {
        ScheduleJson = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkScheduledExecution()
    {
        LastScheduledAt = DateTime.UtcNow;
    }

    public WorkflowDefinition Clone(Guid userId, string? newName = null)
    {
        return new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            Name = newName ?? $"{Name} (Copy)",
            Description = Description,
            DefinitionJson = DefinitionJson,
            ScheduleJson = null, // Don't copy schedule
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }
}
