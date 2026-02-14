namespace OpenClaw.Contracts.Users.Dtos;

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string Status,
    List<string> Roles,
    List<string> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt);
