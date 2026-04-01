using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Weda.Core.Domain;

namespace Weda.Core.Infrastructure.Middleware;

public class EventualConsistencyMiddleware<TDbContext>(RequestDelegate next)
    where TDbContext : DbContext
{
    public async Task InvokeAsync(HttpContext context, IPublisher publisher, TDbContext dbContext)
    {
        // Skip transaction if endpoint is marked with [SkipTransaction]
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<SkipTransactionAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var transaction = await dbContext.Database.BeginTransactionAsync();
        context.Response.OnCompleted(async () =>
        {
            try
            {
                if (context.Items.TryGetValue(EventualConsistencyMiddlewareConstants.DomainEventsKey, out var value) && value is Queue<IDomainEvent> domainEvents)
                {
                    while (domainEvents.TryDequeue(out var nextEvent))
                    {
                        await publisher.Publish(nextEvent);
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        });

        await next(context);
    }
}
