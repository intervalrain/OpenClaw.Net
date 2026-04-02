using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Updates.Responses;

namespace OpenClaw.Contracts.Updates.Queries;

public record GetPendingUpdatesQuery : IRequest<ErrorOr<IReadOnlyList<SystemUpdateResponse>>>;

public record GetAllUpdatesQuery : IRequest<ErrorOr<IReadOnlyList<SystemUpdateResponse>>>;
