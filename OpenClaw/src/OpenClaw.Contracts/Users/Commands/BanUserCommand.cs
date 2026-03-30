using ErrorOr;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Users.Commands;

public record BanUserCommand(Guid UserId, string Reason) : ICommand<ErrorOr<Success>>;
