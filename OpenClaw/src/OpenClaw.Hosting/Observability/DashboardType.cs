namespace OpenClaw.Hosting.Observability;

public enum DashboardType
{
    /// <summary>
    /// Aspire Dashboard - Simple standalone dashboard
    /// docker run -d -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
    /// </summary>
    Aspire,

    /// <summary>
    /// Jaeger - Distributed tracing
    /// docker run -d -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one:latest
    /// </summary>
    Jaeger,

    /// <summary>
    /// Prometheus + Grafana - Metrics focused
    /// Requires separate Prometheus and Grafana setup
    /// </summary>
    Prometheus,

    /// <summary>
    /// Custom OTLP collector
    /// </summary>
    Custom
}
