namespace OpenClaw.Contracts.Auth.Requests;

public record RegisterRequest(
    string Email,
    string Password,
    string Name);
