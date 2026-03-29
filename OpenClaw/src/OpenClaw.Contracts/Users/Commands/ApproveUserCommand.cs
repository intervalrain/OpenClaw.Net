using ErrorOr;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Users.Commands;

public record ApproveUserCommand(Guid UserId) : ICommand<ErrorOr<Success>>;
