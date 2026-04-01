namespace Weda.Core.Infrastructure.Messaging.Nats.Caching;

public class NatsKvCacheOptions
{
    public string BucketName { get; set; } = "cache";
    public string? ConnectionName { get; set; }
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(1);
}