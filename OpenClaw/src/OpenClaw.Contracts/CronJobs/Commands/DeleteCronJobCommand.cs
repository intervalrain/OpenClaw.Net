using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.CronJobs.Commands;

public record DeleteCronJobCommand(
    Guid Id,
    Guid UserId) : IRequest<ErrorOr<bool>>;
