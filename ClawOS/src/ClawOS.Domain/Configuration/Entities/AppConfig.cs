using Weda.Core.Domain;

namespace ClawOS.Domain.Configuration.Entities;

public class AppConfig : Entity<Guid>
{
    public string Key { get; private set; } = string.Empty;
    public string? Value { get; private set; }
    public bool IsSecret { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AppConfig() : base(Guid.NewGuid()) { }

    public static AppConfig Create(string key, string? value, bool isSecret = false)
    {
        return new AppConfig
        {
            Key = key,
            Value = value,
            IsSecret = isSecret,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetValue(string? value, bool? isSecret = null)
    {
        Value = value;
        if (isSecret.HasValue)
            IsSecret = isSecret.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}