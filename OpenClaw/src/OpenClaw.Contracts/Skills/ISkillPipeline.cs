namespace OpenClaw.Contracts.Skills;

public interface ISkillPipeline
{
    string Name { get; }
    string Description { get; }

    /// <summary>
    /// JSON Schema defining configurable parameters for this pipeline.
    /// Returns null if no parameters are configurable.
    /// </summary>
    object? Parameters => null;

    Task<SkillPipelineResult> RunAsync(
        PipelineExecutionContext context,
        Func<PipelineApprovalRequest, Task<bool>>? onApprovalRequired = null,
        CancellationToken ct = default);
}

/// <summary>
/// Context for pipeline execution, containing user information captured at request time.
/// </summary>
public record PipelineExecutionContext(
    Guid? UserId,
    string? ArgsJson = null);

public record SkillPipelineResult(
    bool IsSuccess,
    string Summary,
    IReadOnlyList<SkillStepResult> Steps);

public record SkillStepResult(
    string StepName,
    bool IsSuccess,
    string? Output = null,
    string? Error = null);

public record PipelineApprovalRequest(
    string StepName,
    string Description,
    IReadOnlyList<ProposedChange> ProposedChanges);

public record ProposedChange(
    int WorkItemId,
    string Title,
    string WorkItemType,
    string CurrentState,
    string ProposedState,
    string Reason,
    IReadOnlyList<string>? RelatedCommits = null,
    string? WorkItemUrl = null);
