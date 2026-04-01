namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

/// <summary>
/// NATS configuration options that can be bound from appsettings.json
/// </summary>
public class NatsOptions
{
    public const string SectionName = "Nats";

    /// <summary>
    /// The default connection name to use when no connection is specified
    /// </summary>
    public string DefaultConnection { get; set; } = "default";

    /// <summary>
    /// Named NATS connections configuration
    /// </summary>
    public Dictionary<string, NatsConnectionConfig> Connections { get; set; } = [];
}