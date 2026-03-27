using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Contracts.Workflows;

/// <summary>
/// Base class for all workflow nodes.
/// Uses polymorphic JSON serialization based on the Type discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StartNode), "start")]
[JsonDerivedType(typeof(EndNode), "end")]
[JsonDerivedType(typeof(SkillNode), "skill")]
[JsonDerivedType(typeof(ApprovalNode), "approval")]
[JsonDerivedType(typeof(WaitNode), "wait")]
public abstract record WorkflowNode
{
    public required string Id { get; init; }
    public required NodePosition Position { get; init; }
    public string? Label { get; init; }
}

/// <summary>
/// Start node - entry point of the workflow. Each workflow must have exactly one.
/// </summary>
public record StartNode : WorkflowNode;

/// <summary>
/// End node - exit point of the workflow. Each workflow must have at least one.
/// </summary>
public record EndNode : WorkflowNode;

/// <summary>
/// Skill execution node - invokes an IAgentSkill.
/// </summary>
public record SkillNode : WorkflowNode
{
    public required string SkillName { get; init; }

    /// <summary>
    /// Arguments for the skill. Each argument has a priority-based source.
    /// </summary>
    public Dictionary<string, ArgSource>? Args { get; init; }

    /// <summary>
    /// Timeout in seconds for skill execution. Default is 300 (5 minutes).
    /// </summary>
    public int TimeoutSeconds { get; init; } = 300;
}

/// <summary>
/// Wait (sync barrier) node - waits for ALL upstream nodes to complete before continuing.
/// Without a Wait node, downstream nodes fire when ANY upstream node completes.
/// </summary>
public record WaitNode : WorkflowNode;

/// <summary>
/// Approval gate node - pauses workflow execution until approved or rejected.
/// </summary>
public record ApprovalNode : WorkflowNode
{
    /// <summary>
    /// Unique name for this approval gate (e.g., "AB_Approval", "CD_Approval").
    /// </summary>
    public required string ApprovalName { get; init; }

    /// <summary>
    /// Description shown to the approver.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Behavior when workflow is triggered by scheduler.
    /// </summary>
    public ApprovalBehavior ScheduledBehavior { get; init; } = ApprovalBehavior.WaitForApproval;
}

/// <summary>
/// Behavior for approval nodes when workflow is executed by scheduler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApprovalBehavior>))]
public enum ApprovalBehavior
{
    /// <summary>
    /// Pause and wait for human approval (sends notification).
    /// </summary>
    WaitForApproval,

    /// <summary>
    /// Automatically approve when triggered by scheduler.
    /// </summary>
    AutoApprove,

    /// <summary>
    /// Automatically reject when triggered by scheduler (skips downstream nodes).
    /// </summary>
    AutoReject
}