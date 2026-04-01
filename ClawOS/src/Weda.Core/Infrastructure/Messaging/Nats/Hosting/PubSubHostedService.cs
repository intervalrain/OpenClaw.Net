using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weda.Core.Infrastructure.Audit;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Core.Infrastructure.Messaging.Nats.Discovery;

namespace Weda.Core.Infrastructure.Messaging.Nats.Hosting;

public class PubSubHostedService(
    EventControllerDiscovery discovery,
    INatsConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<PubSubHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoints = discovery.CorePubSubEndpoints;

        if (endpoints.Count == 0)
        {
            logger.LogInformation("No Core Pub-Sub endpoints found");
            return;
        }

        logger.LogInformation("Starting Core Pub-Sub subscriptions...");

        var tasks = new List<Task>();

        foreach (var endpoint in endpoints)
        {
            var task = SubscribeAsync(endpoint, stoppingToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private async Task SubscribeAsync(EndpointDescriptor endpoint, CancellationToken stoppingToken)
    {
        var connection = connectionProvider.GetConnection(endpoint.ConnectionName);
        var subject = TemplateResolver.Resolve(endpoint.SubjectPattern, endpoint.ControllerType);

        logger.LogInformation(
            "Core Pub-Sub: {Subject} -> {Controller}.{Method}",
            subject,
            endpoint.ControllerType.Name,
            endpoint.Method.Name);

        await foreach (var msg in connection.SubscribeAsync<byte[]>(subject, cancellationToken: stoppingToken))
        {
            _ = Task.Run(
                async () =>
                {
                    // Extract trace context from headers for logging scope
                    var traceContext = msg.Headers.GetTraceContext();
                    using var logScope = logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["TraceId"] = traceContext.TraceId,
                        ["RequestId"] = traceContext.RequestId
                    });

                    await using var scope = scopeFactory.CreateAsyncScope();
                    var invoker = scope.ServiceProvider.GetRequiredService<EventControllerInvoker>();

                    try
                    {
                        logger.LogDebug("Processing Core Pub-Sub: {Subject}", msg.Subject);

                        await invoker.InvokeAsync(
                            endpoint,
                            msg.Data,
                            msg.Subject,
                            msg.Headers,
                            stoppingToken);

                        logger.LogDebug("Core Pub-Sub processed: {Subject}", msg.Subject);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing Core Pub-Sub: {Subject}", msg.Subject);
                    }
                },
                stoppingToken);
        }
    }
}
