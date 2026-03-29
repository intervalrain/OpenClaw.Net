using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Responses;

namespace OpenClaw.Contracts.CronJobs.Commands;

public record UpdateCronJobCommand(
    Guid Id,
    string? Name,
    string? ScheduleJson,
    Guid? SessionId,
    string? WakeMode,
    string? ContextJson,
    string? Content,
    bool? IsActive,
    Guid UserId) : IRequest<ErrorOr<CronJobResponse>>;
