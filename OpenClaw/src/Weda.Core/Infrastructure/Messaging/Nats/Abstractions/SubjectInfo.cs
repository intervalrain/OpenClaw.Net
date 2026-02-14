namespace Weda.Core.Infrastructure.Messaging.Nats.Abstractions;

public record SubjectInfo
{
    /// <summary>
    /// The protocol marker (e.g. "eco1j", "eco1p")
    /// </summary>
    public required string Protocol { get; init; }

    /// <summary>
    /// The serialization type of thet content.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The version of protocol.
    /// </summary>
    public int Version { get; init; }

    public IReadOnlyDictionary<string, string> Segments { get; init; } = new Dictionary<string, string>();
}