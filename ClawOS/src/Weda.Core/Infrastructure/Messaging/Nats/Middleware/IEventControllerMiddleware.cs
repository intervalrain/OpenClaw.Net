namespace Weda.Core.Infrastructure.Messaging.Nats.Middleware;

/// <summary>
/// Middleware interface for EventController pipeline.
/// Used for cross-cutting concerns like logging scope, metrics, etc.
/// </summary>
public interface IEventControllerMiddleware
{
    Task InvokeAsync(EventControllerContext context, Func<Task> next);
}