namespace ClawOS.Contracts.Configuration;

/// <summary>
/// Per-user configuration store. Values are encrypted by default.
/// For sensitive settings like API keys, PAT tokens, etc.
/// </summary>
public interface IUserConfigStore
{
    Task<string?> GetAsync(Guid userId, string key, CancellationToken ct = default);
    Task<string> GetRequiredAsync(Guid userId, string key, CancellationToken ct = default);
    Task SetAsync(Guid userId, string key, string? value, bool isSecret = true, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid userId, string key, CancellationToken ct = default);
    Task<List<UserConfigEntry>> GetAllAsync(Guid userId, CancellationToken ct = default);
}

public record UserConfigEntry(string Key, string? Value, bool IsSecret);
