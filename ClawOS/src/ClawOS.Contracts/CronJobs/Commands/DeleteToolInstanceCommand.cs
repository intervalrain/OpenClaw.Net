using ErrorOr;
using Mediator;

namespace ClawOS.Contracts.CronJobs.Commands;

public record DeleteToolInstanceCommand(
    Guid Id,
    Guid UserId) : IRequest<ErrorOr<bool>>;
