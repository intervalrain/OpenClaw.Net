using Weda.Core.Domain;

namespace OpenClaw.Domain.Users.Entities;

public class UserPreference : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string? Value { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private UserPreference() : base(Guid.NewGuid()) { }

    public static UserPreference Create(Guid userId, string key, string? value)
    {
        return new UserPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = key.ToLowerInvariant(),
            Value = value,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetValue(string? value)
    {
        Value = value;
        UpdatedAt = DateTime.UtcNow;
    }
}
