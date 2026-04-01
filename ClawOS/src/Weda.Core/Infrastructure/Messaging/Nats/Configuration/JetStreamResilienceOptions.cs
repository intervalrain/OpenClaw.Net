namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

/// <summary>
/// Options for JetStream resilience (Retry + Circuit Breaker).
/// </summary>
public class JetStreamResilienceOptions
{
    /// <summary>
    /// Maximum retry attempts for JsPublishAsync. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff. Default: 1 second.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Circuit breaker failure ratio threshold. Default: 0.5 (50%).
    /// </summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Circuit breaker sampling duration. Default: 30 seconds.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Circuit breaker break duration. Default: 30 seconds.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum throughput before circuit breaker activates. Default: 10.
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;
}