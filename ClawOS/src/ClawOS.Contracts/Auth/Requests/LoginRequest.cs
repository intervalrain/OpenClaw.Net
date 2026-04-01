namespace ClawOS.Contracts.Auth.Requests;

public record LoginRequest(
    string Email,
    string Password);
