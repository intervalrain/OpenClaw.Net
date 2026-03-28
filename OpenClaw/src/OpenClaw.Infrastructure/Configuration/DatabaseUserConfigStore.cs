using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Security;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Infrastructure.Configuration;

public class DatabaseUserConfigStore(
    IUserConfigRepository repository,
    IEncryptionService encryptionService,
    IUnitOfWork uow) : IUserConfigStore
{
    public async Task<string?> GetAsync(Guid userId, string key, CancellationToken ct = default)
    {
        var config = await repository.GetByKeyAsync(userId, key, ct);
        if (config is null) return null;

        return config.IsSecret && config.Value is not null
            ? encryptionService.Decrypt(config.Value)
            : config.Value;
    }

    public async Task<string> GetRequiredAsync(Guid userId, string key, CancellationToken ct = default)
    {
        return await GetAsync(userId, key, ct)
            ?? throw new InvalidOperationException($"Required user configuration '{key}' is not set.");
    }

    public async Task SetAsync(Guid userId, string key, string? value, bool isSecret = true, CancellationToken ct = default)
    {
        var config = await repository.GetByKeyAsync(userId, key, ct);

        var storedValue = isSecret && value is not null
            ? encryptionService.Encrypt(value)
            : value;

        if (config is null)
        {
            config = UserConfig.Create(userId, key, storedValue, isSecret);
            await repository.AddAsync(config, ct);
        }
        else
        {
            config.SetValue(storedValue, isSecret);
            await repository.UpdateAsync(config, ct);
        }

        await uow.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid userId, string key, CancellationToken ct = default)
    {
        var config = await repository.GetByKeyAsync(userId, key, ct);
        if (config is null) return false;

        await repository.DeleteAsync(config, ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<UserConfigEntry>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var configs = await repository.GetAllByUserAsync(userId, ct);

        return configs.Select(c => new UserConfigEntry(
            c.Key,
            c.IsSecret && c.Value is not null ? encryptionService.Decrypt(c.Value) : c.Value,
            c.IsSecret
        )).ToList();
    }
}
