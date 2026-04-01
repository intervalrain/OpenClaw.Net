using System.Text.Json;

namespace Weda.Core.Presentation.Wedally.Contracts;

/// <summary>
/// Response from publishing a NATS message through Wedally.
/// </summary>
public record NatsPublishResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The subject that was published to.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Response data from Request-Reply operation (if successful).
    /// </summary>
    public JsonElement? ResponseData { get; init; }

    /// <summary>
    /// Error code if the operation failed.
    /// </summary>
    public int? ErrorCode { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Elapsed time in milliseconds.
    /// </summary>
    public long ElapsedMs { get; init; }
}
