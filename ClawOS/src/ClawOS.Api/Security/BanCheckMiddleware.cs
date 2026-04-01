using System.Collections.Concurrent;
using System.Text.Json;

using ClawOS.Domain.Users.Enums;
using ClawOS.Domain.Users.Repositories;

namespace ClawOS.Api.Security;

/// <summary>
/// Checks if the authenticated user is banned on every request.
/// Uses in-memory cache with short TTL to avoid hitting DB on every request.
/// Cache is immediately invalidated when a ban/unban action occurs.
/// </summary>
public class BanCheckMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Cache: userId → (isBanned, banReason, expiresAt)
    private static readonly ConcurrentDictionary<Guid, BanCacheEntry> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst("id")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                var entry = await GetOrRefreshCacheAsync(userId, context.RequestServices);

                if (entry.IsBanned)
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        code = "User.AccountBanned",
                        reason = entry.BanReason ?? "Your account has been banned.",
                        banned = true
                    }, JsonOptions));
                    return;
                }
            }
        }

        await next(context);
    }

    private static async Task<BanCacheEntry> GetOrRefreshCacheAsync(Guid userId, IServiceProvider services)
    {
        if (_cache.TryGetValue(userId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached;
        }

        var userRepository = services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(userId);

        var entry = new BanCacheEntry(
            IsBanned: user?.Status == UserStatus.Banned,
            BanReason: user?.BanReason,
            ExpiresAt: DateTime.UtcNow.Add(CacheTtl));

        _cache[userId] = entry;
        return entry;
    }

    /// <summary>
    /// Immediately invalidate cache for a user (call after ban/unban).
    /// </summary>
    public static void InvalidateUser(Guid userId)
    {
        _cache.TryRemove(userId, out _);
    }

    private record BanCacheEntry(bool IsBanned, string? BanReason, DateTime ExpiresAt);
}
