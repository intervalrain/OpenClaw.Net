using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Classifies tool calls for automatic permission approval.
/// Used in unattended execution (CronJobs) to decide if a tool call
/// is safe to auto-approve without human confirmation.
///
/// Ref: Claude Code yoloClassifier.ts — ML-powered semantic approval
/// with circuit breaker for safety.
/// </summary>
public interface ITranscriptClassifier
{
    /// <summary>
    /// Returns whether the tool call should be auto-approved.
    /// </summary>
    Task<ClassificationResult> ClassifyAsync(
        string toolName,
        string? arguments,
        ToolContext context,
        CancellationToken ct = default);
}

public record ClassificationResult(bool IsApproved, string? Reason = null);
