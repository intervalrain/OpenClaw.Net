using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Queries;
using OpenClaw.Contracts.CronJobs.Responses;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.CronJobs.Repositories;

namespace OpenClaw.Application.CronJobs.Queries;

public class GetCronJobsQueryHandler(
    ICronJobRepository repository) : IRequestHandler<GetCronJobsQuery, ErrorOr<IReadOnlyList<CronJobResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<CronJobResponse>>> Handle(
        GetCronJobsQuery request,
        CancellationToken ct)
    {
        IReadOnlyList<CronJob> jobs;

        if (request.UserId.HasValue)
        {
            jobs = await repository.GetAllByUserAsync(request.UserId.Value, ct);
        }
        else
        {
            // When no user filter, get scheduled jobs as a fallback
            jobs = await repository.GetScheduledJobsAsync(ct);
        }

        if (request.IsActive.HasValue)
        {
            jobs = jobs.Where(j => j.IsActive == request.IsActive.Value).ToList();
        }

        var responses = jobs.Select(ToResponse).ToList();
        return responses;
    }

    internal static CronJobResponse ToResponse(CronJob job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        ScheduleJson = job.ScheduleJson,
        SessionId = job.SessionId,
        WakeMode = job.WakeMode.ToString(),
        ContextJson = job.ContextJson,
        Content = job.Content,
        IsActive = job.IsActive,
        CreatedByUserId = job.CreatedByUserId,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt,
        LastExecution = job.Executions
            .OrderByDescending(e => e.StartedAt)
            .Select(e => GetCronJobExecutionsQueryHandler.ToResponse(e))
            .FirstOrDefault()
    };
}

public class GetCronJobQueryHandler(
    ICronJobRepository repository) : IRequestHandler<GetCronJobQuery, ErrorOr<CronJobResponse>>
{
    public async ValueTask<ErrorOr<CronJobResponse>> Handle(
        GetCronJobQuery request,
        CancellationToken ct)
    {
        var job = await repository.GetByIdAsync(request.Id, ct);
        if (job is null)
        {
            return Error.NotFound($"CronJob {request.Id} not found");
        }

        if (job.CreatedByUserId != request.UserId)
        {
            return Error.NotFound($"CronJob {request.Id} not found");
        }

        return GetCronJobsQueryHandler.ToResponse(job);
    }
}
