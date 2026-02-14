using System.Text.Json;

namespace Weda.Core.Presentation.Wedally.Contracts;

/// <summary>
/// Request to publish a NATS message through Wedally.
/// </summary>
public record NatsPublishRequest
{
    /// <summary>
    /// The endpoint ID to publish to (e.g., "EmployeeEventController_GetEmployee").
    /// </summary>
    public required string EndpointId { get; init; }

    /// <summary>
    /// The complete subject to publish to (e.g., "employee.v1.123.get").
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Optional JSON payload to send with the message.
    /// </summary>
    public JsonElement? Payload { get; init; }

    /// <summary>
    /// Timeout in milliseconds for Request-Reply operations.
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;
}
