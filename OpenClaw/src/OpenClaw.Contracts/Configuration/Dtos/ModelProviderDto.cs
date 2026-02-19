namespace OpenClaw.Contracts.Configuration.Dtos;

public record ModelProviderDto(
    Guid Id,
    string Type,
    string Name,
    string Url,
    string ModelName,
    string? ApiKeyMasked,
    bool IsActive,
    DateTime CreatedAt);