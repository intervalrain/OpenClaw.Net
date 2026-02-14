using NATS.Client.Core;
using NATS.Client.JetStream.Models;

namespace Weda.Core.Application.Interfaces.Messaging;

/// <summary>
/// NATS client interface with automatic trace header injection.
/// All methods automatically inject TraceId/RequestId from AuditContextAccessor.Current.
/// </summary>
public interface IJetStreamClient
{
    /// <summary>
    /// Publishes a message using Core NATS (fire-and-forget).
    /// </summary>
    Task PublishAsync<T>(string subject, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request and waits for a response (Request-Reply pattern).
    /// </summary>
    Task<TReply> RequestAsync<TRequest, TReply>(string subject, TRequest data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request with timeout and returns the full NatsMsg for detailed response handling.
    /// </summary>
    Task<NatsMsg<TReply>> RequestAsync<TRequest, TReply>(
        string subject,
        TRequest data,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to JetStream for persistent delivery.
    /// </summary>
    Task<PubAckResponse> JsPublishAsync<T>(string subject, T data, CancellationToken cancellationToken = default);
}