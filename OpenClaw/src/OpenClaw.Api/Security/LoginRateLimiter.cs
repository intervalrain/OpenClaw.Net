using System.Collections.Concurrent;

namespace OpenClaw.Api.Security;

/// <summary>
/// In-memory login attempt tracker for brute-force protection.
/// Locks accounts after too many failed attempts.
/// </summary>
public class LoginRateLimiter
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, LoginAttemptInfo> _attempts = new();

    public bool IsLockedOut(string key)
    {
        if (!_attempts.TryGetValue(key, out var info))
            return false;

        if (info.LockedUntil.HasValue && info.LockedUntil > DateTime.UtcNow)
            return true;

        // Clean up expired lockout
        if (info.LockedUntil.HasValue && info.LockedUntil <= DateTime.UtcNow)
        {
            _attempts.TryRemove(key, out _);
            return false;
        }

        return false;
    }

    public TimeSpan? GetRemainingLockout(string key)
    {
        if (!_attempts.TryGetValue(key, out var info) || !info.LockedUntil.HasValue)
            return null;

        var remaining = info.LockedUntil.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public void RecordFailedAttempt(string key)
    {
        _attempts.AddOrUpdate(key,
            _ => new LoginAttemptInfo { FailedAttempts = 1, FirstAttemptAt = DateTime.UtcNow },
            (_, existing) =>
            {
                // Reset if window expired
                if (DateTime.UtcNow - existing.FirstAttemptAt > AttemptWindow)
                {
                    return new LoginAttemptInfo { FailedAttempts = 1, FirstAttemptAt = DateTime.UtcNow };
                }

                existing.FailedAttempts++;

                if (existing.FailedAttempts >= MaxAttempts)
                {
                    existing.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                }

                return existing;
            });
    }

    public void RecordSuccessfulLogin(string key)
    {
        _attempts.TryRemove(key, out _);
    }

    private class LoginAttemptInfo
    {
        public int FailedAttempts { get; set; }
        public DateTime FirstAttemptAt { get; set; }
        public DateTime? LockedUntil { get; set; }
    }
}
