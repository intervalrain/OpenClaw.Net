namespace OpenClaw.Contracts.Auth.Responses;

/// <summary>
/// Registration response for pending approval workflow
/// </summary>
public record RegisterResponse(
    Guid Id,
    string Email,
    string Name,
    string Status,
    string Message);
