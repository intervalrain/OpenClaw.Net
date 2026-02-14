namespace OpenClaw.Contracts.Users.Requests;

public record UpdateUserRequest(
    string? Name = null,
    string? Password = null);
