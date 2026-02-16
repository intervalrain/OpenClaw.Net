namespace OpenClaw.Hosting.Observability;

public class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// Enable OpenTelemetry. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Service name for telemetry. Default: "OpenClaw"
    /// </summary>
    public string ServiceName { get; set; } = "OpenClaw";

    /// <summary>
    /// Service version. Default: "1.0.0"
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// OTLP exporter endpoint.
    /// Examples:
    /// - Aspire Dashboard: http://localhost:4317
    /// - Jaeger: http://localhost:4317
    /// - Custom collector: http://your-collector:4317
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Dashboard type for display purposes.
    /// Options: Aspire, Jaeger, Prometheus, Custom
    /// </summary>
    public DashboardType Dashboard { get; set; } = DashboardType.Aspire;
}
