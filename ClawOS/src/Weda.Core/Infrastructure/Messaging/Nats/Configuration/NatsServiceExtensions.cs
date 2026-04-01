using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    // Track registered assemblies to support incremental registration
    private static readonly HashSet<Assembly> _registeredAssemblies = [];
    private static EventControllerDiscovery? _sharedDiscovery;
    private static readonly object _lock = new();

    public static IServiceCollection AddEventControllers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly must be provided", nameof(assemblies));
        }

        lock (_lock)
        {
            // Filter out already registered assemblies
            var newAssemblies = assemblies.Where(a => !_registeredAssemblies.Contains(a)).ToArray();
            if (newAssemblies.Length == 0)
            {
                return services; // All assemblies already registered
            }

            // Create or reuse shared discovery
            _sharedDiscovery ??= new EventControllerDiscovery();
            _sharedDiscovery.DiscoverControllers(newAssemblies);

            // Track registered assemblies
            foreach (var assembly in newAssemblies)
            {
                _registeredAssemblies.Add(assembly);
            }

            // Always update the singleton registration with the shared discovery
            // Remove any existing registration first
            var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(EventControllerDiscovery));
            if (existingDescriptor != null)
            {
                services.Remove(existingDescriptor);
            }
            services.AddSingleton(_sharedDiscovery);

            // Auto-register all discovered EventController types in DI
            var controllerTypes = _sharedDiscovery.Endpoints
                .Select(e => e.ControllerType)
                .Distinct();
            foreach (var controllerType in controllerTypes)
            {
                services.TryAddScoped(controllerType);
            }
        }

        // Register invoker (creates controller instances and invokes methods)
        services.TryAddScoped<EventControllerInvoker>();

        // Register message handler for NAK + DLQ support
        services.TryAddSingleton<JetStreamMessageHandler>();

        // Register default consumer options if not configured
        services.TryAddSingleton(Options.Create(new JetStreamConsumerOptions()));

        // Register all 4 HostedServices (TryAdd to avoid duplicates)
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RequestReplyHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, PubSubHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, JetStreamConsumeHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, JetStreamFetchHostedService>());

        return services;
    }
}