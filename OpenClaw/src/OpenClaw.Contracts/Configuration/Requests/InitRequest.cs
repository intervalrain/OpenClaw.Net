namespace OpenClaw.Contracts.Configuration.Requests;

public record InitRequest(string Email, string Password, string? Name);