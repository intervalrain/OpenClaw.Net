namespace OpenClaw.Contracts.Workflows;

/// <summary>
/// Store for managing workflow execution state and approval waiting.
/// </summary>
public interface IWorkflowExecutionStore
{
    /// <summary>
    /// Sets a node as pending approval and returns when approval is received.
    /// </summary>
    Task SetPendingApprovalAsync(
        Guid executionId,
        string nodeId,
        string approvalName,
        string? description,
        CancellationToken ct);

    /// <summary>
    /// Waits for approval decision on a specific node.
    /// Returns true if approved, false if rejected.
    /// </summary>
    Task<bool> WaitForApprovalAsync(
        Guid executionId,
        string nodeId,
        CancellationToken ct);

    /// <summary>
    /// Submits an approval decision for a node.
    /// </summary>
    Task SubmitApprovalAsync(
        Guid executionId,
        string nodeId,
        bool approved,
        CancellationToken ct);

    /// <summary>
    /// Gets the pending approval info for an execution, if any.
    /// </summary>
    Task<(string NodeId, string ApprovalName, string? Description)?> GetPendingApprovalAsync(
        Guid executionId,
        CancellationToken ct);
}
