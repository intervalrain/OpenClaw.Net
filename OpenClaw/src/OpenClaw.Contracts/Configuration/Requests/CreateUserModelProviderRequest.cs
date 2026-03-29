namespace OpenClaw.Contracts.Configuration.Requests;

public record CreateUserModelProviderRequest(
    Guid? GlobalModelProviderId,
    string? Type,
    string Name,
    string? Url,
    string? ModelName,
    string? ApiKey,
    bool IsDefault = false);
