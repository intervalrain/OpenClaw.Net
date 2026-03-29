namespace OpenClaw.Contracts.Configuration.Dtos;

public record UserModelProviderDto(
    Guid Id,
    string Type,
    string Name,
    string Url,
    string ModelName,
    string? ApiKeyMasked,
    Guid? GlobalModelProviderId,
    string? GlobalProviderName,
    bool IsDefault,
    DateTime CreatedAt);
