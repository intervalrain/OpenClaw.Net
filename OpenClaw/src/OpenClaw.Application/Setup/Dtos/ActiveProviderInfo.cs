namespace OpenClaw.Contracts.Setup.Dtos;

public record ActiveProviderInfo(
    Guid Id,
    string Type,
    string Name,
    string ModelName);