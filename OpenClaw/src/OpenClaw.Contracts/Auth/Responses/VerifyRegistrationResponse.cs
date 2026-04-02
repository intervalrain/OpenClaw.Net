namespace OpenClaw.Contracts.Auth.Responses;

public record VerifyRegistrationResponse(Guid Id,
    string Email,
    string Name,
    string Status,
    string Message) : RegisterResponse(Id, Email, Name, Status, Message);