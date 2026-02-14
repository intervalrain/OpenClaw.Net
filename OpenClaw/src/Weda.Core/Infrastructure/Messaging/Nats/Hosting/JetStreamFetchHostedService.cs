using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NATS.Client.JetStream;

using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Core.Infrastructure.Messaging.Nats.Discovery;

namespace Weda.Core.Infrastructure.Messaging.Nats.Hosting;

public class JetStreamFetchHostedService(
    EventControllerDiscovery discovery,
    INatsConnectionProvider connectionProvider,
    JetStreamMessageHandler msgHandler,
    ILogger<JetStreamFetchHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoints = discovery.JetStreamFetchEndpoints;

        if (endpoints.Count == 0)
        {
            logger.LogInformation("No JetStream Fetch endpoints found");
            return;
        }

        logger.LogInformation("Starting JetStream Fetch (FetchAsync) consumers...");

        var tasks = endpoints.Select(e => FetchAsync(e, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task FetchAsync(EndpointDescriptor endpoint, CancellationToken stoppingToken)
    {
        var js = connectionProvider.GetJetStreamContext(endpoint.ConnectionName);

        try
        {
            var consumer = await msgHandler.SetupConsumerAsync(js, endpoint, stoppingToken);
            if (consumer is null) return;

            logger.LogInformation(
                "JetStream Fetch: {Subject} (Stream: {Stream}, Consumer: {Consumer}) -> {Controller}.{Method}",
                TemplateResolver.Resolve(endpoint.SubjectPattern, endpoint.ControllerType),
                endpoint.StreamName,
                endpoint.ConsumerName,
                endpoint.ControllerType.Name,
                endpoint.Method.Name);

            // Fetch mode: continuously fetch batches
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var msg in consumer.FetchAsync<byte[]>(
                        opts: new NatsJSFetchOpts { MaxMsgs = 10, Expires = TimeSpan.FromSeconds(5) },
                        cancellationToken: stoppingToken))
                    {
                        _ = Task.Run(
                            async () => await msgHandler.HandleAsync(msg, endpoint, js, stoppingToken),
                            stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error fetching messages from {Consumer}", endpoint.ConsumerName);
                    await Task.Delay(1000, stoppingToken); 
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to setup JetStream Fetch consumer {Consumer} on stream {Stream}",
                endpoint.ConsumerName,
                endpoint.StreamName);
        }
    }
}