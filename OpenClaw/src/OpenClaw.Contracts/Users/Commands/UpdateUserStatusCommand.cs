using ErrorOr;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Users.Commands;

public record UpdateUserStatusCommand(Guid UserId, string Status) : ICommand<ErrorOr<Success>>;
