namespace Weda.Core.Application.Security.Models;

public record CurrentUser(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Roles);
