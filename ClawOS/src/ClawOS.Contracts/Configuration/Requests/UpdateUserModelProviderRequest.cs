namespace ClawOS.Contracts.Configuration.Requests;

public record UpdateUserModelProviderRequest(
    string Name,
    string Url,
    string ModelName,
    string? ApiKey);
