using System.Collections.Concurrent;

namespace OpenClaw.Api.Security;

public class RegistrationRateLimiter
{
    private const int MaxRegistrationAttempts = 3;
    private const int MaxResendAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, AttemptInfo> _registerAttempts = new();
    private readonly ConcurrentDictionary<string, AttemptInfo> _resendAttempts = new();

    public bool IsRegistrationLimited(string email)
        => IsLimited(_registerAttempts, email, MaxRegistrationAttempts);

    public bool IsResendLimited(string email)
        => IsLimited(_resendAttempts, email, MaxResendAttempts);

    public void RecordRegistration(string email)
        => Record(_registerAttempts, email);

    public void RecordResend(string email)
        => Record(_resendAttempts, email);

    private static bool IsLimited(ConcurrentDictionary<string, AttemptInfo> store, string key, int max)
    {
        if (!store.TryGetValue(key, out var info))
            return false;

        if (DateTime.UtcNow - info.FirstAttemptAt > Window)
        {
            store.TryRemove(key, out _);
            return false;
        }

        return info.Count >= max;
    }

    private static void Record(ConcurrentDictionary<string, AttemptInfo> store, string key)
    {
        store.AddOrUpdate(key,
            _ => new AttemptInfo { Count = 1, FirstAttemptAt = DateTime.UtcNow },
            (_, existing) =>
            {
                if (DateTime.UtcNow - existing.FirstAttemptAt > Window)
                    return new AttemptInfo { Count = 1, FirstAttemptAt = DateTime.UtcNow };

                existing.Count++;
                return existing;
            });
    }

    private class AttemptInfo
    {
        public int Count { get; set; }
        public DateTime FirstAttemptAt { get; set; }
    }
}