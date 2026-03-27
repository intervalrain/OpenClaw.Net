using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Queries;
using OpenClaw.Contracts.CronJobs.Responses;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.CronJobs.Repositories;

namespace OpenClaw.Application.CronJobs.Queries;

public class GetCronJobExecutionsQueryHandler(
    ICronJobExecutionRepository repository) : IRequestHandler<GetCronJobExecutionsQuery, ErrorOr<IReadOnlyList<CronJobExecutionResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<CronJobExecutionResponse>>> Handle(
        GetCronJobExecutionsQuery request,
        CancellationToken ct)
    {
        var executions = request.CronJobId.HasValue
            ? await repository.GetByCronJobIdAsync(request.CronJobId.Value, request.Limit, request.Offset, ct)
            : await repository.GetRecentAsync(request.Limit, request.Offset, ct);

        var responses = executions.Select(ToResponse).ToList();
        return responses;
    }

    internal static CronJobExecutionResponse ToResponse(CronJobExecution execution) => new()
    {
        Id = execution.Id,
        CronJobId = execution.CronJobId,
        Status = execution.Status.ToString(),
        Trigger = execution.Trigger.ToString(),
        OutputText = execution.OutputText,
        ToolCallsJson = execution.ToolCallsJson,
        ErrorMessage = execution.ErrorMessage,
        StartedAt = execution.StartedAt,
        CompletedAt = execution.CompletedAt
    };
}
