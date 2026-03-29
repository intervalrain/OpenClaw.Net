using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.CronJobs.Commands;

public record DeleteToolInstanceCommand(
    Guid Id,
    Guid UserId) : IRequest<ErrorOr<bool>>;
