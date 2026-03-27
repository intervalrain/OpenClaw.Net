using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Responses;

namespace OpenClaw.Contracts.CronJobs.Commands;

public record CreateCronJobCommand(
    string Name,
    string? ScheduleJson,
    Guid? SessionId,
    string WakeMode,
    string? ContextJson,
    string Content,
    Guid UserId) : IRequest<ErrorOr<CronJobResponse>>;
