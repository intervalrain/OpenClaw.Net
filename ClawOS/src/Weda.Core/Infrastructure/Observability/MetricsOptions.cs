namespace Weda.Core.Infrastructure.Observability;

public class MetricsOptions
{
    public bool Enabled { get; set; } = true;
    public bool UseConsoleExporter { get; set; } = false;
    public string? OtlpEndpoint { get; set; }
}