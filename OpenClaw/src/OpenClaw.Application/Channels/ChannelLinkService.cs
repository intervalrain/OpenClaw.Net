using System.Collections.Concurrent;
using System.Security.Cryptography;
using OpenClaw.Domain.Channels.Entities;
using OpenClaw.Domain.Channels.Repositories;

namespace OpenClaw.Application.Channels;

/// <summary>
/// Manages channel user binding via verification codes.
/// Flow: 1) User sends /link in Telegram → gets a 6-digit code
///       2) User enters code in Web UI → binding created
/// </summary>
public class ChannelLinkService(IChannelUserBindingRepository repository)
{
    // Pending verifications: code → (platform, externalUserId, displayName, expiresAt)
    private static readonly ConcurrentDictionary<string, PendingLink> PendingLinks = new();

    public record PendingLink(string Platform, string ExternalUserId, string? DisplayName, DateTime ExpiresAt);

    /// <summary>
    /// Generate a verification code for a channel user requesting to link.
    /// Called from Telegram /link command.
    /// </summary>
    public string GenerateVerificationCode(string platform, string externalUserId, string? displayName = null)
    {
        // Remove expired entries
        var expired = PendingLinks.Where(kv => kv.Value.ExpiresAt < DateTime.UtcNow).Select(kv => kv.Key).ToList();
        foreach (var key in expired) PendingLinks.TryRemove(key, out _);

        // Check if already pending
        var existing = PendingLinks.FirstOrDefault(kv =>
            kv.Value.Platform == platform.ToLowerInvariant() && kv.Value.ExternalUserId == externalUserId);
        if (existing.Key is not null && existing.Value.ExpiresAt > DateTime.UtcNow)
            return existing.Key;

        // Generate 6-digit code
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var pending = new PendingLink(platform.ToLowerInvariant(), externalUserId, displayName, DateTime.UtcNow.AddMinutes(5));
        PendingLinks[code] = pending;
        return code;
    }

    /// <summary>
    /// Verify a code and create the binding. Called from Web UI.
    /// </summary>
    public async Task<ChannelUserBinding?> VerifyAndLinkAsync(string code, Guid openClawUserId, CancellationToken ct = default)
    {
        if (!PendingLinks.TryRemove(code, out var pending))
            return null;

        if (pending.ExpiresAt < DateTime.UtcNow)
            return null;

        // Check if already bound
        var existing = await repository.GetByExternalUserAsync(pending.Platform, pending.ExternalUserId, ct);
        if (existing is not null)
            return existing; // Already bound

        var binding = ChannelUserBinding.Create(pending.Platform, pending.ExternalUserId, openClawUserId, pending.DisplayName);
        await repository.AddAsync(binding, ct);
        return binding;
    }

    /// <summary>
    /// Unlink a channel binding.
    /// </summary>
    public async Task<bool> UnlinkAsync(Guid openClawUserId, string platform, string externalUserId, CancellationToken ct = default)
    {
        var binding = await repository.GetByExternalUserAsync(platform, externalUserId, ct);
        if (binding is null || binding.OpenClawUserId != openClawUserId)
            return false;

        await repository.DeleteAsync(binding, ct);
        return true;
    }

    /// <summary>
    /// Resolve an external user to an OpenClaw user ID.
    /// Returns null if not bound.
    /// </summary>
    public async Task<Guid?> ResolveUserAsync(string platform, string externalUserId, CancellationToken ct = default)
    {
        var binding = await repository.GetByExternalUserAsync(platform, externalUserId, ct);
        return binding?.OpenClawUserId;
    }
}
