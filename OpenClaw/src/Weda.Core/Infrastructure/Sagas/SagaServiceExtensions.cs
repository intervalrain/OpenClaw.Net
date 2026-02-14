using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Weda.Core.Application.Interfaces;

namespace Weda.Core.Infrastructure.Sagas;

public static class SagaServiceExtensions
{
    public static IServiceCollection AddSagas(this IServiceCollection services)
    {
        services.TryAddSingleton<ISagaStateStore, CacheSagaStateStore>();
        services.TryAddScoped<SagaOrchestrator>();
        return services;
    }
}