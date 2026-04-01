namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

/// <summary>
/// Configuration for a single NATS connection
/// </summary>
public class NatsConnectionConfig
{
    /// <summary>
    /// NATS server URL (e.g., "nats://localhost:4222")
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Optional connection name for logging/debugging
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to NATS credentials file (.creds)
    /// </summary>
    public string? CredsFile { get; set; }

    /// <summary>
    /// Username for basic authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for basic authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Authentication token
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// NKey public key string for NKey authentication
    /// </summary>
    public string? NKey { get; set; }

    /// <summary>
    /// Path to NKey file for NKey authentication
    /// </summary>
    public string? NKeyFile { get; set; }

    /// <summary>
    /// JWT token for JWT authentication
    /// </summary>
    public string? Jwt { get; set; }

    /// <summary>
    /// Seed for NKey authentication
    /// </summary>
    public string? Seed { get; set; }
}