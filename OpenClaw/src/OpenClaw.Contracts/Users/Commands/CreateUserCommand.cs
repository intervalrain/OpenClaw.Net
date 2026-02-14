using ErrorOr;
using Weda.Core.Application.Interfaces;
using OpenClaw.Contracts.Users.Dtos;

namespace OpenClaw.Contracts.Users.Commands;

public record CreateUserCommand(
    string Email,
    string Password,
    string Name) : ICommand<ErrorOr<UserDto>>;
