using ErrorOr;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Contracts.Users.Commands;

public record RejectUserCommand(Guid UserId) : ICommand<ErrorOr<Success>>;
