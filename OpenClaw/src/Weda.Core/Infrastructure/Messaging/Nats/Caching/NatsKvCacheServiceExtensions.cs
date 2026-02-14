using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Weda.Core.Infrastructure.Messaging.Nats.Caching;

public static class NatsKvCacheServiceExtensions
{
    public static IServiceCollection AddNatsKvCache(
        this IServiceCollection services,
        Action<NatsKvCacheOptions>? configure = null)
    {
        var options = new NatsKvCacheOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IDistributedCache, NatsKvDistributedCache>();

        return services;
    }
}