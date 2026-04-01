using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Core.Infrastructure.Messaging.Nats.Discovery;

namespace Weda.Core.Infrastructure.Messaging.Nats.Hosting;

public class JetStreamConsumeHostedService(
    EventControllerDiscovery discovery,
    INatsConnectionProvider connectionProvider,
    JetStreamMessageHandler msgHandler,
    ILogger<JetStreamConsumeHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoints = discovery.JetStreamConsumeEndpoints;

        if (endpoints.Count == 0)
        {
            logger.LogInformation("No JetStream Consume endpoints found");
            return;
        }

        logger.LogInformation("Starting JetStream Consume (ConsumeAsync) subscriptions...");

        var tasks = endpoints.Select(e => SubscribeAsync(e, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task SubscribeAsync(EndpointDescriptor endpoint, CancellationToken stoppingToken)
    {
        var js = connectionProvider.GetJetStreamContext(endpoint.ConnectionName);

        try
        {
            var consumer = await msgHandler.SetupConsumerAsync(js, endpoint, stoppingToken);
            if (consumer is null) return;
            
            logger.LogInformation(
                "JetStream Consume: {Subject} (Stream: {Stream}, Consumer: {Consumer}) -> {Controller}.{Method}",
                TemplateResolver.Resolve(endpoint.SubjectPattern, endpoint.ControllerType),
                endpoint.StreamName,
                endpoint.ConsumerName,
                endpoint.ControllerType.Name,
                endpoint.Method.Name);

            await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: stoppingToken))
            {
                _ = Task.Run(
                    async () => await msgHandler.HandleAsync(msg, endpoint, js, stoppingToken),
                    stoppingToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to setup JetStream Consume consumer {Consumer} on stream {Stream}",
                endpoint.ConsumerName,
                endpoint.StreamName);
        }
    }
}
