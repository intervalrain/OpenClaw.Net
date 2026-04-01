namespace ClawOS.Contracts.Configuration.Dtos;

public record ModelProviderDto(
    Guid Id,
    string Type,
    string Name,
    string Url,
    string ModelName,
    string? ApiKeyMasked,
    string? Description,
    bool AllowUserOverride,
    bool IsActive,
    DateTime CreatedAt);