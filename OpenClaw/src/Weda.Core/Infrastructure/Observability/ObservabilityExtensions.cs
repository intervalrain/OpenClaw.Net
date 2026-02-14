using System.Security.Authentication.ExtendedProtection;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Weda.Core.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        ObservabilityOptions options)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion);

        if (options.Tracing.Enabled)
        {
            services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.SetResourceBuilder(resourceBuilder)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();

                    if (options.Tracing.UseConsoleExporter)
                    {
                        tracing.AddConsoleExporter();
                    }

                    if (!string.IsNullOrEmpty(options.Tracing.OtlpEndpoint))
                    {
                        tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.Tracing.OtlpEndpoint));
                    }
                });
        }

        if (options.Metrics.Enabled)
        {
            services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.SetResourceBuilder(resourceBuilder)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();

                    if (options.Metrics.UseConsoleExporter)
                    {
                        metrics.AddConsoleExporter();
                    }

                    if (!string.IsNullOrEmpty(options.Metrics.OtlpEndpoint))
                    {
                        metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.Metrics.OtlpEndpoint));
                    }
                });
        }

        return services;
    }
}