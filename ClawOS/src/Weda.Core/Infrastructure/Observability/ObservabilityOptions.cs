using Microsoft.Extensions.Logging;


namespace Weda.Core.Infrastructure.Observability;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string ServiceName { get; set; } = "WedaService";
    public string? ServiceVersion { get; set; }

    public TracingOptions Tracing { get; set; } = new();
    public MetricsOptions Metrics { get; set; } = new();
}