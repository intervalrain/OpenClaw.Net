using ErrorOr;
using Weda.Core.Application.Interfaces;
using ClawOS.Contracts.Users.Dtos;

namespace ClawOS.Contracts.Users.Commands;

public record UpdateUserRolesCommand(
    Guid Id,
    List<string> Roles,
    List<string>? Permissions = null) : ICommand<ErrorOr<UserDto>>;
