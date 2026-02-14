using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Weda.Core.Infrastructure.Messaging.Nats.Discovery;
using Weda.Core.Infrastructure.Messaging.Nats.Hosting;

namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

public static class NatsServiceExtensions
{
    public static IServiceCollection AddNats(
        this IServiceCollection services,
        Action<NatsBuilder> configure)
    {
        var builder = new NatsBuilder(services);
        configure(builder);
        builder.Build();

        return services;
    }

    public static IServiceCollection AddEventControllers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly must be provided", nameof(assemblies));
        }

        // Register discovery as singleton (scans assemblies once at startup)
        var discovery = new EventControllerDiscovery();
        discovery.DiscoverControllers(assemblies);
        services.AddSingleton(discovery);

        // Auto-register all discovered EventController types in DI
        var controllerTypes = discovery.Endpoints
            .Select(e => e.ControllerType)
            .Distinct();
        foreach (var controllerType in controllerTypes)
        {
            services.AddScoped(controllerType);
        }

        // Register invoker (creates controller instances and invokes methods)
        services.AddScoped<EventControllerInvoker>();

        // Register message handler for NAK + DLQ support
        services.AddSingleton<JetStreamMessageHandler>();

        // Register default consumer options if not configured
        services.TryAddSingleton(Options.Create(new JetStreamConsumerOptions()));

        // Register all 4 HostedServices
        services.AddHostedService<RequestReplyHostedService>();
        services.AddHostedService<PubSubHostedService>();
        services.AddHostedService<JetStreamConsumeHostedService>();
        services.AddHostedService<JetStreamFetchHostedService>();

        return services;
    }
}