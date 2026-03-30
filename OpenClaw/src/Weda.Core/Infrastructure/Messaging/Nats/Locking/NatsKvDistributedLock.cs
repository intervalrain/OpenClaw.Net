using System.Text;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;

namespace Weda.Core.Infrastructure.Messaging.Nats.Locking;

/// <summary>
/// Distributed lock using NATS KV with CAS (Compare-And-Swap).
/// Used for leader election (e.g., only one scheduler instance runs).
///
/// Lock format: "{instanceId}|{expiresAtUtcTicks}"
/// Renewal: lock holder periodically refreshes the TTL.
/// </summary>
public class NatsKvDistributedLock(
    INatsConnectionProvider connectionProvider,
    ILogger<NatsKvDistributedLock> logger,
    string? connectionName = null)
{
    private const string BucketName = "openclaw_locks";
    private INatsKVStore? _store;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly string InstanceId = $"{Environment.MachineName}_{Environment.ProcessId}";

    /// <summary>
    /// Try to acquire a named lock with a TTL.
    /// Returns true if this instance now holds the lock.
    /// </summary>
    public async Task<bool> TryAcquireAsync(string lockName, TimeSpan ttl, CancellationToken ct = default)
    {
        var store = await GetOrCreateStoreAsync(ct);
        var key = SanitizeKey(lockName);
        var expiresAt = DateTime.UtcNow.Add(ttl);
        var lockValue = $"{InstanceId}|{expiresAt.Ticks}";

        try
        {
            // Try to read existing lock
            var entry = await store.GetEntryAsync<string>(key, cancellationToken: ct);
            var existing = entry.Value;

            if (existing is not null)
            {
                var parts = existing.Split('|');
                if (parts.Length == 2 && long.TryParse(parts[1], out var ticks))
                {
                    var existingExpires = new DateTime(ticks, DateTimeKind.Utc);

                    // Lock is held by us — renew
                    if (parts[0] == InstanceId)
                    {
                        await store.UpdateAsync(key, lockValue, entry.Revision, cancellationToken: ct);
                        return true;
                    }

                    // Lock is held by someone else and not expired
                    if (existingExpires > DateTime.UtcNow)
                        return false;
                }

                // Lock expired — try to take over via CAS
                try
                {
                    await store.UpdateAsync(key, lockValue, entry.Revision, cancellationToken: ct);
                    logger.LogInformation("Acquired expired lock '{Lock}' from {PreviousHolder}", lockName, parts[0]);
                    return true;
                }
                catch (NatsJSApiException)
                {
                    // CAS conflict — another instance got it first
                    return false;
                }
            }
        }
        catch (NatsKVKeyNotFoundException)
        {
            // Key doesn't exist — try to create
        }
        catch (NatsKVKeyDeletedException)
        {
            // Key was deleted — try to create
        }

        // Create new lock
        try
        {
            await store.CreateAsync(key, lockValue, cancellationToken: ct);
            logger.LogInformation("Acquired lock '{Lock}' for instance {Instance}", lockName, InstanceId);
            return true;
        }
        catch (NatsJSApiException)
        {
            // Someone else created it first
            return false;
        }
    }

    /// <summary>
    /// Release a lock (only if held by this instance).
    /// </summary>
    public async Task ReleaseAsync(string lockName, CancellationToken ct = default)
    {
        var store = await GetOrCreateStoreAsync(ct);
        var key = SanitizeKey(lockName);

        try
        {
            var entry = await store.GetEntryAsync<string>(key, cancellationToken: ct);
            if (entry.Value?.StartsWith(InstanceId) == true)
            {
                await store.DeleteAsync(key, cancellationToken: ct);
                logger.LogInformation("Released lock '{Lock}'", lockName);
            }
        }
        catch (NatsKVKeyNotFoundException) { }
        catch (NatsKVKeyDeletedException) { }
    }

    private async Task<INatsKVStore> GetOrCreateStoreAsync(CancellationToken ct)
    {
        if (_store is not null) return _store;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_store is not null) return _store;

            var js = connectionProvider.GetJetStreamContext(connectionName);
            var kv = new NatsKVContext(js);

            try
            {
                _store = await kv.GetStoreAsync(BucketName, ct);
            }
            catch
            {
                _store = await kv.CreateStoreAsync(new NatsKVConfig(BucketName)
                {
                    Description = "OpenClaw distributed locks",
                    MaxAge = TimeSpan.FromHours(1) // Auto-expire stale entries
                }, ct);
            }

            return _store;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string SanitizeKey(string key)
        => System.Text.RegularExpressions.Regex.Replace(key, @"[^A-Za-z0-9_]", "_");
}
