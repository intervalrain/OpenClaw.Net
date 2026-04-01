namespace ClawOS.Contracts.Configuration;

public interface IConfigStore
{
    string? Get(string key);
    string GetRequired(string key);
    T? Get<T>(string key) where T : class;
    Task SetAsync(string key, string? value, bool isSecret = false, CancellationToken ct = default);
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);
    Task<List<ConfigEntry>> GetAllAsync(CancellationToken ct = default);
}

public record ConfigEntry(string Key, string? Value, bool IsSecret);