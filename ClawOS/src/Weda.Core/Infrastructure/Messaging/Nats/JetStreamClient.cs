using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

using Polly;

using Weda.Core.Application.Interfaces.Messaging;
using Weda.Core.Infrastructure.Audit;

namespace Weda.Core.Infrastructure.Messaging.Nats;

/// <summary>
/// NATS client with automatic trace header injection and resilience (Retry + Circuit Breaker).
/// Created via IJetStreamClientFactory.Create().
/// </summary>
public class JetStreamClient : IJetStreamClient
{
    private readonly INatsConnection _connection;
    private readonly NatsJSContext _jetStream;
    private readonly ResiliencePipeline _resiliencePipeline;

    internal JetStreamClient(
        INatsConnection connection,
        NatsJSContext jetStream,
        ResiliencePipeline resiliencePipeline)
    {
        _connection = connection;
        _jetStream = jetStream;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task PublishAsync<T>(string subject, T data, CancellationToken cancellationToken = default)
    {
        var headers = CreateTracedHeaders();
        await _connection.PublishAsync(subject, data, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<TReply> RequestAsync<TRequest, TReply>(
        string subject,
        TRequest data,
        CancellationToken cancellationToken = default)
    {
        var msg = await RequestAsync<TRequest, TReply>(subject, data, Timeout.InfiniteTimeSpan, cancellationToken);
        return msg.Data!;
    }

    public async Task<NatsMsg<TReply>> RequestAsync<TRequest, TReply>(
        string subject,
        TRequest data,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var headers = CreateTracedHeaders();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            cts.CancelAfter(timeout);
        }

        return await _connection.RequestAsync<TRequest, TReply>(
            subject, data, headers: headers, cancellationToken: cts.Token);
    }

    public async Task<PubAckResponse> JsPublishAsync<T>(
        string subject,
        T data,
        CancellationToken cancellationToken = default)
    {
        var headers = CreateTracedHeaders();

        return await _resiliencePipeline.ExecuteAsync(
            async ct => await _jetStream.PublishAsync(subject, data, headers: headers, cancellationToken: ct),
            cancellationToken);
    }

    private static NatsHeaders CreateTracedHeaders()
    {
        var context = AuditContextAccessor.Current;
        return new NatsHeaders().WithTraceContext(TraceContext.Create(context?.TraceId));
    }
}