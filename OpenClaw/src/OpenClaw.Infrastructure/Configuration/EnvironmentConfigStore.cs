using OpenClaw.Contracts.Configuration;

namespace OpenClaw.Infrastructure.Configuration;

public class EnvironmentConfigStore(string? envFilePath = null) : IConfigStore
{
    private readonly Dictionary<string, string> _values = ConfigLoader.LoadEnvFile(envFilePath);

    public string? Get(string key) => Environment.GetEnvironmentVariable(key) ?? _values.GetValueOrDefault(key);

    public string GetRequired(string key) => Get(key) ?? throw new InvalidOperationException($"Required configuration '{key}' is not set.");
    
    public T? Get<T>(string key) where T : class
    {
        var value = Get(key);
        if (value is null) return null;

        return typeof(T) == typeof(string) ? (T)(object)value : null;
    }
}