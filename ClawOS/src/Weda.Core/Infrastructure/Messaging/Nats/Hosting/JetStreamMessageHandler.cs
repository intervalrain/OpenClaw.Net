using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

using Weda.Core.Infrastructure.Audit;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Core.Infrastructure.Messaging.Nats.Discovery;
using Weda.Core.Infrastructure.Messaging.Nats.Exceptions;

namespace Weda.Core.Infrastructure.Messaging.Nats.Hosting;

public class JetStreamMessageHandler(
    IServiceScopeFactory scopeFactory,
    IOptions<JetStreamConsumerOptions> consumerOptions,
    ILogger<JetStreamMessageHandler> logger)
{
    private readonly JetStreamConsumerOptions _options = consumerOptions.Value;

    public async Task<INatsJSConsumer?> SetupConsumerAsync(
        NatsJSContext js,
        EndpointDescriptor endpoint,
        CancellationToken cancellationToken)
    {
        if (endpoint.StreamName is null || endpoint.ConsumerName is null)
        {
            logger.LogWarning(
                "JetStream endpoint {Controller}.{Method} missing StreamName or ConsumerName",
                endpoint.ControllerType.Name,
                endpoint.Method.Name);
            return null;
        }

        var subject = TemplateResolver.Resolve(endpoint.SubjectPattern, endpoint.ControllerType);

        await EnsureStreamExistsAsync(js, endpoint.StreamName, subject, cancellationToken);
        await EnsureConsumerExistsAsync(js, endpoint.StreamName, endpoint.ConsumerName, cancellationToken);

        return await js.GetConsumerAsync(endpoint.StreamName, endpoint.ConsumerName, cancellationToken);
    }

    public async Task HandleAsync(
        INatsJSMsg<byte[]> msg,
        EndpointDescriptor endpoint,
        NatsJSContext js,
        CancellationToken cancellationToken = default)
    {
        var traceContext = msg.Headers.GetTraceContext();
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceContext.TraceId,
            ["ReuqestId"] = traceContext.RequestId
        });

        await using var scope = scopeFactory.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<EventControllerInvoker>();

        try
        {
            logger.LogDebug("Processing JetStream message: {Subject}, stream: {Stream}",
                msg.Subject, endpoint.StreamName);

            await invoker.InvokeAsync(
                endpoint,
                msg.Data,
                msg.Subject,
                msg.Headers,
                cancellationToken);

            await msg.AckAsync(cancellationToken: cancellationToken);

            logger.LogDebug("Jet stream message processed: {Subject}", msg.Subject);
        }
        catch (TransientException ex)
        {
            await HandleTransientErrorAsync(msg, endpoint.StreamName!, js, ex, cancellationToken);
        }
        catch (Exception ex)
        {
            await HandleNonTransientErrorAsync(msg, endpoint.StreamName!, js, ex, cancellationToken);
        }
    }

    private async Task HandleTransientErrorAsync(INatsJSMsg<byte[]> msg, string streamName, NatsJSContext js, TransientException ex, CancellationToken cancellationToken)
    {
        var numDelivered = msg.Metadata?.NumDelivered ?? 1;

        if (numDelivered < _options.MaxRedeliveries)
        {
            logger.LogWarning(
                ex,
                "Transient error process {Subject}, NAK for retry ({Attempt}/{Max})",
                msg.Subject, numDelivered, _options.MaxRedeliveries);

            await msg.NakAsync(delay: _options.NakDelay, cancellationToken: cancellationToken);
        }
        else
        {
            logger.LogError(
                ex,
                "Max redeliveries reached for {Subject}, sending to DLQ",
                msg.Subject);
            
            await SendToDlqAsync(js, streamName, msg, ex.Message, cancellationToken);
            await msg.AckAsync(cancellationToken: cancellationToken);
        }
    }

    private async Task HandleNonTransientErrorAsync(INatsJSMsg<byte[]> msg, string streamName, NatsJSContext js, Exception ex, CancellationToken cancellationToken)
    {
        logger.LogError(ex, "Error processing JetStream message: {Subject}", msg.Subject);

        if (_options.EnableDlq)
        {
            await SendToDlqAsync(js, streamName, msg, ex.Message, cancellationToken);
        }

        await msg.AckAsync(cancellationToken: cancellationToken);
    }

    private async Task SendToDlqAsync(NatsJSContext js, string streamName, INatsJSMsg<byte[]> msg, string message, CancellationToken cancellationToken)
    {
        if (!_options.EnableDlq) return;

        var dlqStreamName = $"{streamName}{_options.DqlStreamSuffix}";
        var dlqSubject = $"{msg.Subject}.dlq";

        try
        {
            await EnsureDlqStreamExistsAsync(js, dlqStreamName, cancellationToken);

            var headers = msg.Headers ?? new NatsHeaders();
            headers["X-Dlq-Error"] = message;
            headers["X-Dlq-Subject"] = msg.Subject;
            headers["X-Dlq-Timestamp"] = DateTime.UtcNow.ToString("O");

            await js.PublishAsync(dlqSubject, msg.Data, headers: headers, cancellationToken: cancellationToken);

            logger.LogInformation("Message sent to DQL: {DlqSubject} (Stream: {DlqStream})",
                dlqSubject, dlqStreamName);
            
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message to DLQ: {DlqStream}", dlqStreamName);
        }
    }

    private async Task EnsureDlqStreamExistsAsync(NatsJSContext js, string dlqStreamName, CancellationToken cancellationToken)
    {
        try
        {
            await js.GetStreamAsync(dlqStreamName, cancellationToken: cancellationToken);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            logger.LogInformation("Creating DLQ stream: {Stream}", dlqStreamName);

            var streamConfig = new StreamConfig
            {
                Name = dlqStreamName,
                Subjects = [$"*.dlq"],
                Retention = StreamConfigRetention.Limits,
                MaxAge = TimeSpan.FromDays(30)
            };

            await js.CreateStreamAsync(streamConfig, cancellationToken);
        }
    }

    private async Task EnsureStreamExistsAsync(
        NatsJSContext js,
        string streamName,
        string subject,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = await js.GetStreamAsync(streamName, cancellationToken: cancellationToken);
            var currentSubjects = stream.Info.Config.Subjects ?? [];
            if (!currentSubjects.Contains(subject))
            {
                var updatedSubjects = currentSubjects.Append(subject).ToList();
                var updatedConfig = stream.Info.Config with { Subjects = updatedSubjects };
                await js.UpdateStreamAsync(updatedConfig, cancellationToken: cancellationToken);
                logger.LogInformation("Added subject {Subject} to stream {Stream}", subject, streamName);
            }
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            logger.LogInformation("Creating stream {Stream} with subject {Subject}", streamName, subject);

            var streamConfig = new StreamConfig
            {
                Name = streamName,
                Subjects = [subject],
            };

            await js.CreateStreamAsync(streamConfig, cancellationToken);
        }
    }

    private async Task EnsureConsumerExistsAsync(
        NatsJSContext js,
        string streamName,
        string consumerName,
        CancellationToken cancellationToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            Name = consumerName,
            DurableName = consumerName,
            AckPolicy = ConsumerConfigAckPolicy.Explicit
        };

        await js.CreateOrUpdateConsumerAsync(streamName, consumerConfig, cancellationToken);
        logger.LogDebug("Consumer {Consumer} on stream {Stream} ensured", consumerName, streamName);
    }
}