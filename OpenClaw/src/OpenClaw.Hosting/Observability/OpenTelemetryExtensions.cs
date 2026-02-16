using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenClaw.Hosting.Observability;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddOpenClawTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(OpenTelemetryOptions.SectionName)
            .Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();

        if (!options.Enabled)
        {
            return services;
        }

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion);

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("OpenClaw.Agent");

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("OpenClaw.Agent");

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            });

        return services;
    }

    /// <summary>
    /// Get the dashboard URL based on configuration
    /// </summary>
    public static string GetDashboardUrl(this OpenTelemetryOptions options)
    {
        return options.Dashboard switch
        {
            DashboardType.Aspire => "http://localhost:18888",
            DashboardType.Jaeger => "http://localhost:16686",
            DashboardType.Prometheus => "http://localhost:9090 (Prometheus) / http://localhost:3000 (Grafana)",
            DashboardType.Custom => options.OtlpEndpoint ?? "Not configured",
            _ => "Unknown"
        };
    }
}