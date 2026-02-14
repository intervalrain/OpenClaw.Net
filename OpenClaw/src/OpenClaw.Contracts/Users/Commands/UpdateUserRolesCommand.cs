using ErrorOr;
using Weda.Core.Application.Interfaces;
using OpenClaw.Contracts.Users.Dtos;

namespace OpenClaw.Contracts.Users.Commands;

public record UpdateUserRolesCommand(
    Guid Id,
    List<string> Roles,
    List<string>? Permissions = null) : ICommand<ErrorOr<UserDto>>;
