using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Weda.Core.Application.Interfaces.Storage;

namespace Weda.Core.Infrastructure.Messaging.Nats.ObjectStore;

public static class NatsObjectStoreServiceExtensions
{
    public static IServiceCollection AddNatsObjectStore(
        this IServiceCollection services, 
        Action<NatsObjectStoreOptions>? configure = null)
    {
        var options = new NatsObjectStoreOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IBlobStorage, NatsObjectStorage>();

        return services;
    }
}