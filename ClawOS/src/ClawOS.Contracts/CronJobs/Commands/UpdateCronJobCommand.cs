using ErrorOr;
using Mediator;
using ClawOS.Contracts.CronJobs.Responses;

namespace ClawOS.Contracts.CronJobs.Commands;

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
