using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Queries;
using OpenClaw.Contracts.CronJobs.Responses;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.CronJobs.Repositories;

namespace OpenClaw.Application.CronJobs.Queries;

public class GetCronJobExecutionsQueryHandler(
    ICronJobExecutionRepository repository,
    ICronJobRepository cronJobRepository) : IRequestHandler<GetCronJobExecutionsQuery, ErrorOr<IReadOnlyList<CronJobExecutionResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<CronJobExecutionResponse>>> Handle(
        GetCronJobExecutionsQuery request,
        CancellationToken ct)
    {
        // If querying by cronJobId, verify the user owns that CronJob
        if (request.CronJobId.HasValue)
        {
            var job = await cronJobRepository.GetByIdAsync(request.CronJobId.Value, ct);
            if (job is null || job.CreatedByUserId != request.UserId)
            {
                return Error.NotFound("CronJob not found");
            }
        }

        var executions = request.CronJobId.HasValue
            ? await repository.GetByCronJobIdAsync(request.CronJobId.Value, request.Limit, request.Offset, ct)
            : await repository.GetByUserAsync(request.UserId, request.Limit, request.Offset, ct);

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
