using ClawOS.Contracts.Configuration;
using ClawOS.Contracts.Security;
using ClawOS.Domain.Configuration.Entities;
using ClawOS.Domain.Configuration.Repositories;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Infrastructure.Configuration;

public class DatabaseConfigStore(
    IAppConfigRepository repository,
    IEncryptionService encryptionService,
    IUnitOfWork uow,
    IConfigStore? fallback = null) : IConfigStore
{
    public string? Get(string key)
    {
        // First try database (sync wrapper for interface compatibility)
        var config = repository.GetByKeyAsync(key).GetAwaiter().GetResult();
        if (config is not null)
        {
            return config.IsSecret && config.Value is not null
                ? encryptionService.Decrypt(config.Value)
                : config.Value;
        }

        // Fallback to next store in chain
        return fallback?.Get(key);
    }

    public string GetRequired(string key) =>
        Get(key) ?? throw new InvalidOperationException($"Required configuration '{key}' is not set.");

    public T? Get<T>(string key) where T : class
    {
        var value = Get(key);
        if (value is null) return null;
        return typeof(T) == typeof(string) ? (T)(object)value : null;
    }

    public async Task SetAsync(string key, string? value, bool isSecret = false, CancellationToken ct = default)
    {
        var config = await repository.GetByKeyAsync(key, ct);

        var storedValue = isSecret && value is not null
            ? encryptionService.Encrypt(value)
            : value;

        if (config is null)
        {
            config = AppConfig.Create(key, storedValue, isSecret);
            await repository.AddAsync(config, ct);
        }
        else
        {
            config.SetValue(storedValue, isSecret);
            await repository.UpdateAsync(config, ct);
        }

        await uow.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        var config = await repository.GetByKeyAsync(key, ct);
        if (config is null)
            return false;

        await repository.DeleteAsync(config, ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<ConfigEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var configs = await repository.GetAllAsync(ct);

        var entries = configs.Select(c => new ConfigEntry(
            c.Key,
            c.IsSecret && c.Value is not null ? encryptionService.Decrypt(c.Value) : c.Value,
            c.IsSecret
        )).ToList();

        // Merge with fallback entries (fallback entries won't override database entries)
        if (fallback is not null)
        {
            var fallbackEntries = await fallback.GetAllAsync(ct);
            var existingKeys = entries.Select(e => e.Key).ToHashSet();
            entries.AddRange(fallbackEntries.Where(e => !existingKeys.Contains(e.Key)));
        }

        return entries;
    }
}