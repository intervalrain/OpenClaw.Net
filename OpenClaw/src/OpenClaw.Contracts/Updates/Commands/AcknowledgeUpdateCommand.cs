using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.Updates.Commands;

public record AcknowledgeUpdateCommand(Guid UpdateId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record DismissUpdateCommand(Guid UpdateId, Guid UserId) : IRequest<ErrorOr<Success>>;
