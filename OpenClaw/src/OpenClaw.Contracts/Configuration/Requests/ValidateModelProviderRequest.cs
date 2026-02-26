namespace OpenClaw.Contracts.Configuration.Requests;

public record ValidateModelProviderRequest(
    string Type,
    string Url,
    string ModelName,
    string? ApiKey);