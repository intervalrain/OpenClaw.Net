using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Commands;
using OpenClaw.Contracts.CronJobs.Responses;
using OpenClaw.Domain.CronJobs;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.CronJobs.Commands;

public class CreateCronJobCommandHandler(
    ICronJobRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateCronJobCommand, ErrorOr<CronJobResponse>>
{
    public async ValueTask<ErrorOr<CronJobResponse>> Handle(
        CreateCronJobCommand request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<WakeMode>(request.WakeMode, ignoreCase: true, out var wakeMode))
        {
            return Error.Validation($"Invalid WakeMode '{request.WakeMode}'");
        }

        var job = CronJob.Create(
            request.UserId,
            Guid.Empty,
            request.Name,
            request.ScheduleJson ?? string.Empty,
            request.Content,
            wakeMode,
            request.SessionId,
            request.ContextJson);

        await repository.AddAsync(job, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ToResponse(job);
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
        UpdatedAt = job.UpdatedAt
    };
}
