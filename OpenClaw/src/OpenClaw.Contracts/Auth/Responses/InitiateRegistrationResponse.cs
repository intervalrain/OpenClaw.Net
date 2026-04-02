namespace OpenClaw.Contracts.Auth.Responses;

public record InitiateRegistrationResponse(
    string Email,
    string Message);