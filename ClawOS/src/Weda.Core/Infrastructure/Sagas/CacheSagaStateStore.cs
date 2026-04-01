using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Sagas;

namespace Weda.Core.Infrastructure.Sagas;

public class CacheSagaStateStore(IDistributedCache cache) : ISagaStateStore
{
    public async Task<SagaState<TData>?> GetAsync<TData>(string sagaId, CancellationToken ct = default) where TData : class
    {
        var key = CacheKey(sagaId);
        var json = await cache.GetStringAsync(key, ct);
        return json is null ? null : JsonSerializer.Deserialize<SagaState<TData>>(json);
    }

    public async Task SaveAsync<TData>(SagaState<TData> state, CancellationToken ct = default) where TData : class
    {
        var key = CacheKey(state.SagaId);
        var json = JsonSerializer.Serialize(state);
        await cache.SetStringAsync(key, json, ct);
    }

    public async Task DeleteAsync(string sagaId, CancellationToken ct = default)
    {
        var key = CacheKey(sagaId);
        await cache.RemoveAsync(key, ct);
    }

    private static string CacheKey(string id) => $"saga:{id}";
}