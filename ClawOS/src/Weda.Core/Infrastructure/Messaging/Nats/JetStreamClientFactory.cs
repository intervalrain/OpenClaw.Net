using Microsoft.Extensions.Options;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

using Weda.Core.Application.Interfaces.Messaging;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;

namespace Weda.Core.Infrastructure.Messaging.Nats;

/// <summary>
/// Factory implementation for creating IJetStreamClient instances with resilience.
/// </summary>
public class JetStreamClientFactory : IJetStreamClientFactory
{
    private readonly INatsConnectionProvider _provider;
    private readonly ResiliencePipeline _resiliencePipeline;

    public JetStreamClientFactory(
        INatsConnectionProvider provider,
        IOptions<JetStreamResilienceOptions> options)
    {
        _provider = provider;
        _resiliencePipeline = CreateResiliencePipeline(options.Value);
    }

    public IJetStreamClient Create(string? connection = null)
    {
        var natsConnection = _provider.GetConnection(connection);
        var jetStream = _provider.GetJetStreamContext(connection);
        return new JetStreamClient(natsConnection, jetStream, _resiliencePipeline);
    }

    private static ResiliencePipeline CreateResiliencePipeline(JetStreamResilienceOptions options)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = options.BaseDelay
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.FailureRatio,
                SamplingDuration = options.SamplingDuration,
                BreakDuration = options.BreakDuration,
                MinimumThroughput = options.MinimumThroughput
            })
            .Build();
    }
}