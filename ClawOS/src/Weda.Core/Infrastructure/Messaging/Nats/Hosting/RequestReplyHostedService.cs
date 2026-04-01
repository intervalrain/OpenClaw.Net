using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Services;
using NATS.Net;
using Weda.Core.Infrastructure.Audit;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Core.Infrastructure.Messaging.Nats.Discovery;

namespace Weda.Core.Infrastructure.Messaging.Nats.Hosting;

public class RequestReplyHostedService(
    EventControllerDiscovery discovery,
    INatsConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<RequestReplyHostedService> logger)
    : IHostedService
{
    private readonly List<INatsSvcServer> _services = [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var endpoints = discovery.RequestReplyEndpoints;

        if (endpoints.Count == 0)
        {
            logger.LogInformation("No Request-Reply endpoints found");
            return;
        }

        // Group endpoints by connection to create one service per connection
        var endpointsByConnection = endpoints.GroupBy(e => e.ConnectionName);

        foreach (var group in endpointsByConnection)
        {
            await StartNatsServiceAsync(group.Key, group.ToList(), cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Request-Reply services...");
        foreach (var service in _services)
        {
            await service.StopAsync(cancellationToken);
            await service.DisposeAsync();
        }
    }

    private async Task StartNatsServiceAsync(string connectionName, List<EndpointDescriptor> endpoints, CancellationToken cancellationToken)
    {
        var services = new Dictionary<string, INatsSvcServer>();

        foreach (var endpoint in endpoints)
        {
            var handler = CreateServiceHandler(endpoint);
            var endpointName = $"{endpoint.ControllerType.Name}_{endpoint.Method.Name}";
            var service = await EnsureServerExisted(services, connectionName, endpoint, cancellationToken);
            var subject = TemplateResolver.Resolve(endpoint.SubjectPattern, endpoint.ControllerType);
            await service.AddEndpointAsync(
                handler: handler,
                name: endpointName,
                subject: subject,
                cancellationToken: cancellationToken);

            logger.LogInformation("  ↳ {Subject} -> {Controller}.{Method}", endpoint.SubjectPattern, endpoint.ControllerType.Name, endpoint.Method.Name);
           _services.Add(service);
        }
    }

    private async Task<INatsSvcServer> EnsureServerExisted(Dictionary<string, INatsSvcServer> services, string connectionName, EndpointDescriptor endpoint, CancellationToken cancellationToken)
    {
        var serviceName = TemplateResolver.Resolve(endpoint.ControllerType.Name);
        var connection = connectionProvider.GetConnection(connectionName);

        if (services.TryGetValue(serviceName, out INatsSvcServer? value)) return value;
        
        var config = new NatsSvcConfig(serviceName, "1.0.0");
        var svc = connection.CreateServicesContext();
        var service = await svc.AddServiceAsync(config, cancellationToken);
        services.Add(serviceName, service);
        logger.LogInformation("Started NATS Service: {ServiceName} on {Connection}", serviceName, connectionName);
        
        return service;
    }


    private Func<NatsSvcMsg<byte[]>, ValueTask> CreateServiceHandler(EndpointDescriptor endpoint)
    {
        return async (msg) =>
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
                logger.LogDebug("Processing Request-Reply: {Subject}", msg.Subject);

                var data = msg.Data;

                var response = await invoker.InvokeAsync(
                    endpoint,
                    data,
                    msg.Subject,
                    msg.Headers);

                if (response is not null)
                {
                    await msg.ReplyAsync(response);
                }
                else
                {
                    await msg.ReplyAsync();
                }

                logger.LogDebug("Request-Reply completed: {Subject}", msg.Subject);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: {Subject}", msg.Subject);
                await msg.ReplyErrorAsync(500, ex.Message);
            }
        };
    }
}