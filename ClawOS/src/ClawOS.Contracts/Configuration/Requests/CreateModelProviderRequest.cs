namespace ClawOS.Contracts.Configuration.Requests;

public record CreateModelProviderRequest(
    string Type,
    string Name,
    string Url,
    string ModelName,
    string? ApiKey,
    string? Description = null,
    bool AllowUserOverride = true,
    bool IsActive = false);