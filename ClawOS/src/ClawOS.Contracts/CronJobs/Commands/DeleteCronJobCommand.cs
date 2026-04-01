using ErrorOr;
using Mediator;

namespace ClawOS.Contracts.CronJobs.Commands;

public record DeleteCronJobCommand(
    Guid Id,
    Guid UserId) : IRequest<ErrorOr<bool>>;
