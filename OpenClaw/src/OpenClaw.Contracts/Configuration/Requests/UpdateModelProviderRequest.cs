namespace OpenClaw.Contracts.Configuration.Requests;

public record UpdateModelProviderRequest(
    string Name,
    string Url,
    string ModelName,
    string? ApiKey);