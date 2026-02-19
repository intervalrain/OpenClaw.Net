namespace OpenClaw.Contracts.Configuration.Requests;

public record CreateModelProviderRequest(
    string Type,
    string Name,
    string Url,
    string ModelName,
    string? ApiKey,
    bool IsActive = false);