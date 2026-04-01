using System.Text.Json;

using ErrorOr;

using NATS.Client.ObjectStore;


using Weda.Core.Application.Interfaces.Storage;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;

namespace Weda.Core.Infrastructure.Messaging.Nats.ObjectStore;

public class NatsObjectStorage(
    INatsConnectionProvider connectionProvider,
    NatsObjectStoreOptions options) : IBlobStorage
{
    private INatsObjStore? _store;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    public async Task<ErrorOr<BlobInfo>> PutAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var store = await GetOrCreateStoreAsync(ct);

        var data = SerializeToBytes(value);
        using var stream = new MemoryStream(data);

        var meta = await store.PutAsync(key, stream, cancellationToken: ct);
        return new BlobInfo(meta.Name, meta.Size, meta.MTime.DateTime);
    }

    public async Task<ErrorOr<T>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var store = await GetOrCreateStoreAsync(ct);

        try
        {
            using var ms = new MemoryStream();
            await store.GetAsync(key, ms, cancellationToken: ct);
            ms.Position = 0;

            return DeserializeFromBytes<T>(ms.ToArray());
        }   
        catch (NatsObjNotFoundException)
        {
            return Error.NotFound("Blob.NotFound", $"Blob '{key}' not found");
        } 
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(string key, CancellationToken ct = default)
    {
        var store = await GetOrCreateStoreAsync(ct);

        try
        {
            await store.DeleteAsync(key, ct);
            return Result.Deleted;   
        }
        catch (NatsObjNotFoundException)
        {
             return Error.NotFound("Blob.NotFound", $"Blob '{key}' not found");
        }    
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var store = await GetOrCreateStoreAsync(ct);

        try
        {
            await store.GetInfoAsync(key, cancellationToken: ct);
            return true;   
        }
        catch (NatsObjNotFoundException)
        {
            return false;
        }
    }

    private async Task<INatsObjStore> GetOrCreateStoreAsync(CancellationToken ct)
    {
        if (_store is not null) return _store;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_store is not null) return _store;

            var js = connectionProvider.GetJetStreamContext(options.ConnectionName);
            var objContext = new NatsObjContext(js);

            var config = new NatsObjConfig(options.BucketName);
            _store = await objContext.CreateObjectStoreAsync(config, ct);

            return _store;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static byte[] SerializeToBytes<T>(T? value)
    {
        if (value is byte[] bytes) return bytes;
        if (value is Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }

    private static T DeserializeFromBytes<T>(byte[] data)
    {
        if (typeof(T) == typeof(byte[])) return (T)(object)data;
        return JsonSerializer.Deserialize<T>(data)!;
    }
}