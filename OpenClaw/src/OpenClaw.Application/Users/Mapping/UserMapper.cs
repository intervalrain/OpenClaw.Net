using Riok.Mapperly.Abstractions;

using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.ValueObjects;

namespace OpenClaw.Application.Users.Mapping;

[Mapper]
public static partial class UserMapper
{
    [MapperIgnoreSource(nameof(User.PasswordHash))]
    [MapProperty(nameof(User.Email), nameof(UserDto.Email), Use = nameof(MapEmail))]
    [MapProperty(nameof(User.Roles), nameof(UserDto.Roles), Use = nameof(MapRoles))]
    [MapProperty(nameof(User.Permissions), nameof(UserDto.Permissions), Use = nameof(MapPermissions))]
    public static partial UserDto ToDto(User user);

    public static List<UserDto> ToDtoList(IEnumerable<User> users)
        => users.Select(ToDto).ToList();

    private static string MapEmail(UserEmail email) => email.Value;

    private static List<string> MapRoles(IReadOnlyList<string> roles) => roles.ToList();

    private static List<string> MapPermissions(IReadOnlyList<string> permissions) => permissions.ToList();
}
