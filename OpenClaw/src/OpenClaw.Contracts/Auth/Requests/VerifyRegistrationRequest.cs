namespace OpenClaw.Contracts.Auth.Requests;

public record VerifyRegistrationRequest(string Email, string Code);