using ErrorOr;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Users.Commands;

public record DeleteUserCommand(Guid Id) : ICommand<ErrorOr<Deleted>>;
