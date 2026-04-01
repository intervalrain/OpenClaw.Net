using ErrorOr;
using Weda.Core.Application.Interfaces;
using ClawOS.Contracts.Users.Dtos;

namespace ClawOS.Contracts.Users.Commands;

public record CreateUserCommand(
    string Email,
    string Password,
    string Name) : ICommand<ErrorOr<UserDto>>;
