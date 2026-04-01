using ErrorOr;
using Weda.Core.Application.Interfaces;

namespace ClawOS.Contracts.Users.Commands;

public record DeleteUserCommand(Guid Id) : ICommand<ErrorOr<Deleted>>;
