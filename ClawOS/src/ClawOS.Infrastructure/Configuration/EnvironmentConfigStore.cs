using ClawOS.Contracts.Configuration;

namespace ClawOS.Infrastructure.Configuration;

public class EnvironmentConfigStore(string? envFilePath = null, IConfigStore? fallback = null) : IConfigStore
{
    private readonly Dictionary<string, string> _values = ConfigLoader.LoadEnvFile(envFilePath);

    public string? Get(string key)
    {
        // First try environment variable, then .env file
        var value = Environment.GetEnvironmentVariable(key) ?? _values.GetValueOrDefault(key);
        if (value is not null)
            return value;

        // Fallback to next store in chain
        return fallback?.Get(key);
    }

    public string GetRequired(string key) => Get(key) ?? throw new InvalidOperationException($"Required configuration '{key}' is not set.");

    public T? Get<T>(string key) where T : class
    {
        var value = Get(key);
        if (value is null) return null;

        return typeof(T) == typeof(string) ? (T)(object)value : null;
    }

    public Task SetAsync(string key, string? value, bool isSecret = false, CancellationToken ct = default)
    {
        // Delegate to fallback if available
        if (fallback is not null)
            return fallback.SetAsync(key, value, isSecret, ct);

        throw new NotSupportedException("EnvironmentConfigStore is read-only and no writable fallback is configured.");
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        // Delegate to fallback if available
        if (fallback is not null)
            return fallback.DeleteAsync(key, ct);

        throw new NotSupportedException("EnvironmentConfigStore is read-only and no writable fallback is configured.");
    }

    public async Task<List<ConfigEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var entries = _values
            .Select(kvp => new ConfigEntry(kvp.Key, kvp.Value, false))
            .ToList();

        // Merge with fallback entries
        if (fallback is not null)
        {
            var fallbackEntries = await fallback.GetAllAsync(ct);
            var existingKeys = entries.Select(e => e.Key).ToHashSet();
            entries.AddRange(fallbackEntries.Where(e => !existingKeys.Contains(e.Key)));
        }

        return entries;
    }
}