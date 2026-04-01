using Weda.Core.Domain;

namespace ClawOS.Domain.Users.Entities;

/// <summary>
/// Per-user configuration (encrypted by default).
/// For sensitive settings like API keys, PAT tokens, etc.
/// </summary>
public class UserConfig : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string? Value { get; private set; }
    public bool IsSecret { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private UserConfig() : base(Guid.NewGuid()) { }

    public static UserConfig Create(Guid userId, string key, string? value, bool isSecret = true)
    {
        return new UserConfig
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = key.ToLowerInvariant(),
            Value = value,
            IsSecret = isSecret,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetValue(string? value, bool? isSecret = null)
    {
        Value = value;
        if (isSecret.HasValue) IsSecret = isSecret.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}
