using System.Collections.Concurrent;
using OpenClaw.Contracts.Workflows;

namespace OpenClaw.Infrastructure.Workflows;

/// <summary>
/// In-memory store for managing workflow approval state.
/// Uses TaskCompletionSource for blocking until approval is received.
/// </summary>
public class InMemoryWorkflowApprovalStore : IWorkflowExecutionStore
{
    private record ApprovalResult(bool Approved, string? EditedOutput);

    private record ApprovalWaiter(
        string ApprovalName,
        string? Description,
        TaskCompletionSource<ApprovalResult> Tcs);

    private readonly ConcurrentDictionary<(Guid ExecutionId, string NodeId), ApprovalWaiter> _waiters = new();

    public Task SetPendingApprovalAsync(
        Guid executionId,
        string nodeId,
        string approvalName,
        string? description,
        CancellationToken ct)
    {
        var key = (executionId, nodeId);
        var waiter = new ApprovalWaiter(approvalName, description, new TaskCompletionSource<ApprovalResult>());
        _waiters[key] = waiter;
        return Task.CompletedTask;
    }

    public async Task<(bool Approved, string? EditedOutput)> WaitForApprovalAsync(
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var key = (executionId, nodeId);

        if (!_waiters.TryGetValue(key, out var waiter))
        {
            throw new InvalidOperationException($"No approval waiter found for execution {executionId}, node {nodeId}");
        }

        // Register cancellation
        await using var registration = ct.Register(() =>
            waiter.Tcs.TrySetCanceled(ct));

        try
        {
            var result = await waiter.Tcs.Task;
            return (result.Approved, result.EditedOutput);
        }
        finally
        {
            _waiters.TryRemove(key, out _);
        }
    }

    public Task SubmitApprovalAsync(
        Guid executionId,
        string nodeId,
        bool approved,
        string? editedOutput,
        CancellationToken ct)
    {
        var key = (executionId, nodeId);

        if (_waiters.TryGetValue(key, out var waiter))
        {
            waiter.Tcs.TrySetResult(new ApprovalResult(approved, editedOutput));
        }

        return Task.CompletedTask;
    }

    public Task<(string NodeId, string ApprovalName, string? Description)?> GetPendingApprovalAsync(
        Guid executionId,
        CancellationToken ct)
    {
        var pending = _waiters
            .Where(kvp => kvp.Key.ExecutionId == executionId)
            .Select(kvp => (kvp.Key.NodeId, kvp.Value.ApprovalName, kvp.Value.Description))
            .FirstOrDefault();

        if (pending == default)
        {
            return Task.FromResult<(string, string, string?)?>(null);
        }

        return Task.FromResult<(string, string, string?)?>(pending);
    }
}
