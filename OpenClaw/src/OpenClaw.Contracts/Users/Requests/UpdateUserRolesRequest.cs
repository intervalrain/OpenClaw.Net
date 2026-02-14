namespace OpenClaw.Contracts.Users.Requests;

public record UpdateUserRolesRequest(
    List<string> Roles,
    List<string>? Permissions = null);
