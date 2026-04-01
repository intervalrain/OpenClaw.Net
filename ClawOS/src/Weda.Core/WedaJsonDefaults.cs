using System.Text.Json;

namespace Weda.Core;

/// <summary>
/// Provides default JSON serialization options used throughout the Weda framework.
/// Ensures consistent JSON handling across HTTP APIs, NATS messaging, and other components.
/// </summary>
public static class WedaJsonDefaults
{
    /// <summary>
    /// Default JsonSerializerOptions with camelCase property naming and case-insensitive deserialization.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
