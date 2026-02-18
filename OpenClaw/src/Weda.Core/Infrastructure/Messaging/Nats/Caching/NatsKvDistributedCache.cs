using Microsoft.Extensions.Caching.Distributed;
using NATS.Client.KeyValueStore;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;

namespace Weda.Core.Infrastructure.Messaging.Nats.Caching;

public class NatsKvDistributedCache(
    INatsConnectionProvider connectionProvider,
    NatsKvCacheOptions options) : IDistributedCache
{
    private INatsKVStore? _store;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var store = await GetOrCreateStoreAsync(token);
        var sanitizedKey = SanitizeKey(key);
        try
        {
            var entry = await store.GetEntryAsync<byte[]>(sanitizedKey, cancellationToken: token);
            return entry.Value;
        }
        catch (NatsKVKeyNotFoundException)
        {
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            return null;
        }
    }

    // Not support
    public void Refresh(string key) { }
    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        var store = await GetOrCreateStoreAsync(token);
        var sanitizedKey = SanitizeKey(key);
        try
        {
            await store.DeleteAsync(sanitizedKey, cancellationToken: token);
        }
        catch (NatsKVKeyNotFoundException)
        {
            // ignore
        }
        catch (NatsKVKeyDeletedException)
        {
            // ignore - already deleted
        }
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options).GetAwaiter().GetResult();

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var store = await GetOrCreateStoreAsync(token);
        var sanitizedKey = SanitizeKey(key);
        await store.PutAsync(sanitizedKey, value, cancellationToken: token);
    }

    /// <summary>
    /// NATS KV keys only allow [A-Za-z0-9_], sanitize invalid characters
    /// </summary>
    private static string SanitizeKey(string key) =>
        string.Create(key.Length, key, (span, k) =>
        {
            for (var i = 0; i < k.Length; i++)
            {
                var c = k[i];
                span[i] = char.IsLetterOrDigit(c) || c == '_' ? c : '_';
            }
        });

    private async Task<INatsKVStore> GetOrCreateStoreAsync(CancellationToken token)
    {
        if (_store is not null) return _store;

        await _initLock.WaitAsync(token);
        try
        {
            if (_store is not null) return _store;

            var js = connectionProvider.GetJetStreamContext(options.ConnectionName);
            var kv = new NatsKVContext(js);

            var config = new NatsKVConfig(options.BucketName)
            {
                MaxAge = options.DefaultTtl  
            };

            _store = await kv.CreateStoreAsync(config, token);
            
            return _store;
        }
        finally
        {
            _initLock.Release();
        }
    }
}