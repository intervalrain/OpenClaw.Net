using System.Collections.Concurrent;

using OpenClaw.Contracts.Pipelines;
using OpenClaw.Contracts.Pipelines.Responses;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Infrastructure.Pipelines;

public class InMemoryPipelineExecutionStore : IPipelineExecutionStore
{
    private readonly ConcurrentDictionary<string, PipelineExecution> _executions = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _approvalWaiters = new();

    public Task<PipelineExecution> CreateAsync(string pipelineName, string? argsJson, CancellationToken ct)
    {
        var execution = new PipelineExecution
        {
            Id = Guid.NewGuid().ToString("N"),
            PipelineName = pipelineName,
            ArgsJson = argsJson,
            Status = PipelineExecutionStatus.Running,
            CreatedAt = DateTime.UtcNow
        };

        _executions[execution.Id] = execution;
        return Task.FromResult(execution);
    }

    public Task<PipelineExecution?> GetAsync(string executionId, CancellationToken ct)
    {
        _executions.TryGetValue(executionId, out var execution);
        return Task.FromResult(execution);
    }

    public Task<IReadOnlyList<PipelineExecution>> ListRecentAsync(int limit, CancellationToken ct)
    {
        var recent = _executions.Values
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<PipelineExecution>>(recent);
    }

    public Task UpdateStatusAsync(string executionId, PipelineExecutionStatus status, CancellationToken ct)
    {
        if (_executions.TryGetValue(executionId, out var execution))
        {
            execution.Status = status;
        }
        return Task.CompletedTask;
    }

    public Task SetPendingApprovalAsync(string executionId, PipelineApprovalInfo approvalInfo, CancellationToken ct)
    {
        if (_executions.TryGetValue(executionId, out var execution))
        {
            execution.PendingApproval = approvalInfo;
            _approvalWaiters[executionId] = new TaskCompletionSource<bool>();
        }
        return Task.CompletedTask;
    }

    public Task<bool> SubmitApprovalAsync(string executionId, bool approved, CancellationToken ct)
    {
        if (_executions.TryGetValue(executionId, out var execution))
        {
            execution.ApprovalDecision = approved;

            if (_approvalWaiters.TryRemove(executionId, out var tcs))
            {
                tcs.TrySetResult(approved);
            }

            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async Task<bool> WaitForApprovalAsync(string executionId, CancellationToken ct)
    {
        if (!_approvalWaiters.TryGetValue(executionId, out var tcs))
        {
            return false;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

        if (completedTask == tcs.Task)
        {
            return await tcs.Task;
        }

        return false;
    }

    public Task SetResultAsync(string executionId, ToolPipelineResult result, CancellationToken ct)
    {
        if (_executions.TryGetValue(executionId, out var execution))
        {
            execution.Result = result;
        }
        return Task.CompletedTask;
    }
}
