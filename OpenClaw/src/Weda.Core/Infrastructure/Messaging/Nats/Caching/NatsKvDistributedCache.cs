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
        try
        {
            var entry = await store.GetEntryAsync<byte[]>(key, cancellationToken: token);
            return entry.Value;
        }
        catch (NatsKVKeyNotFoundException)
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
        try
        {
            await store.DeleteAsync(key, cancellationToken: token);
        }
        catch (NatsKVKeyNotFoundException)
        {
            // ignore
        }
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options).GetAwaiter().GetResult();

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var store = await GetOrCreateStoreAsync(token);
        await store.PutAsync(key, value, cancellationToken: token);
    }

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