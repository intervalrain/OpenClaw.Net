namespace OpenClaw.Contracts.Users.Requests;

public record CreateUserRequest(
    string Email,
    string Password,
    string Name);
