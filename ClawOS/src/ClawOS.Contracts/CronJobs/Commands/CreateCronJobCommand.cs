using ErrorOr;
using Mediator;
using ClawOS.Contracts.CronJobs.Responses;

namespace ClawOS.Contracts.CronJobs.Commands;

public record CreateCronJobCommand(
    string Name,
    string? ScheduleJson,
    Guid? SessionId,
    string WakeMode,
    string? ContextJson,
    string Content,
    Guid UserId) : IRequest<ErrorOr<CronJobResponse>>;
