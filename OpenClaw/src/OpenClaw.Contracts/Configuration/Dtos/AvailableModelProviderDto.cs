namespace OpenClaw.Contracts.Configuration.Dtos;

/// <summary>
/// A global model provider available for users to add to their own provider list.
/// </summary>
public record AvailableModelProviderDto(
    Guid Id,
    string Type,
    string Name,
    string ModelName,
    string? Description);
