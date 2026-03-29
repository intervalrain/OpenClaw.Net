namespace OpenClaw.Contracts.Configuration.Requests;

public record UpdateModelProviderRequest(
    string Name,
    string Url,
    string ModelName,
    string? ApiKey,
    string? Description = null,
    bool? AllowUserOverride = null);