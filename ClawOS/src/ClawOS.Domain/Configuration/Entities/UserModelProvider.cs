using Weda.Core.Domain;

namespace ClawOS.Domain.Configuration.Entities;

public class UserModelProvider : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? GlobalModelProviderId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public string? EncryptedApiKey { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private UserModelProvider() : base(Guid.NewGuid()) { }

    /// <summary>
    /// Create a user model provider referencing a global provider (uses global credentials).
    /// </summary>
    public static UserModelProvider CreateFromGlobal(
        Guid userId,
        Guid globalModelProviderId,
        string type,
        string name,
        string url,
        string modelName,
        bool isDefault = false)
    {
        return new UserModelProvider
        {
            UserId = userId,
            GlobalModelProviderId = globalModelProviderId,
            Type = type,
            Name = name,
            Url = url,
            ModelName = modelName,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a custom user model provider with the user's own API key.
    /// </summary>
    public static UserModelProvider CreateCustom(
        Guid userId,
        string type,
        string name,
        string url,
        string modelName,
        string? encryptedApiKey,
        bool isDefault = false)
    {
        return new UserModelProvider
        {
            UserId = userId,
            GlobalModelProviderId = null,
            Type = type,
            Name = name,
            Url = url,
            ModelName = modelName,
            EncryptedApiKey = encryptedApiKey,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string url, string modelName, string? encryptedApiKey)
    {
        Name = name;
        Url = url;
        ModelName = modelName;
        if (encryptedApiKey is not null)
            EncryptedApiKey = encryptedApiKey;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }
}
