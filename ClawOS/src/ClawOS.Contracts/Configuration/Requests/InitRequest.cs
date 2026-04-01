namespace ClawOS.Contracts.Configuration.Requests;

public record InitRequest(string Email, string Password, string? Name);