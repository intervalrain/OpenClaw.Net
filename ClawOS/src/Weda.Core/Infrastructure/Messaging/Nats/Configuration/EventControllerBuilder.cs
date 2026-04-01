using Microsoft.Extensions.DependencyInjection;
using Weda.Core.Infrastructure.Messaging.Nats.Middleware;

namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

/// <summary>
/// Extension methods for configuring EventController middleware on NatsBuilder.
/// </summary>
/// <example>
/// .AddWedaCore(..., nats => nats
///     .Use&lt;AuditLoggingMiddleware&gt;()
///     .Use&lt;MyCustomMiddleware&gt;())
/// </example>
public static class NatsBuilderMiddlewareExtensions
{
    /// <summary>
    /// Adds an EventController middleware to the pipeline.
    /// Middleware executes in the order they are registered.
    /// </summary>
    public static NatsBuilder Use<TMiddleware>(this NatsBuilder builder)
        where TMiddleware : class, IEventControllerMiddleware
    {
        builder.Services.AddScoped<IEventControllerMiddleware, TMiddleware>();
        return builder;
    }
}