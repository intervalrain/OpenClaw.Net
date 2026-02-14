namespace Weda.Core.Application.Interfaces.Messaging;

/// <summary>
/// Factory for creating IJetStreamClient instances.
/// Use this to create clients for different NATS connections.
/// </summary>
public interface IJetStreamClientFactory
{
    /// <summary>
    /// Creates a client for specified connection.
    /// </summary>
    /// <param name="connection">Connection name, or null for default connection.</param>
    IJetStreamClient Create(string? connection = null);
}