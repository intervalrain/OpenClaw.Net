namespace OpenClaw.Contracts.Users.Dtos;

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string Status,
    string? BanReason,
    List<string> Roles,
    List<string> Permissions,
    string? WorkspacePath,
    long? WorkspaceQuotaMb,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt);
