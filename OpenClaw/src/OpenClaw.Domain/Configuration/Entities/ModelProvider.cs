using Weda.Core.Domain;

namespace OpenClaw.Domain.Configuration.Entities;

public class ModelProvider : Entity<Guid>
{
    public string Type { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public string? EncryptedApiKey { get; private set; }
    public string? Description { get; private set; }
    public int? MaxContextTokens { get; private set; }
    public bool AllowUserOverride { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ModelProvider(): base(Guid.NewGuid()) { }

    public static ModelProvider Create(
        string type,
        string name,
        string url,
        string modelName,
        string? encryptedApiKey,
        string? description = null,
        bool allowUserOverride = true,
        bool isActive = false,
        int? maxContextTokens = null)
    {
        return new ModelProvider
        {
            Type = type,
            Name = name,
            Url = url,
            ModelName = modelName,
            EncryptedApiKey = encryptedApiKey,
            Description = description,
            MaxContextTokens = maxContextTokens,
            AllowUserOverride = allowUserOverride,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string url, string modelName, string? encryptedApiKey,
        string? description = null, bool? allowUserOverride = null)
    {
        Name = name;
        Url = url;
        ModelName = modelName;
        if (encryptedApiKey is not null)
            EncryptedApiKey = encryptedApiKey;
        if (description is not null)
            Description = description;
        if (allowUserOverride.HasValue)
            AllowUserOverride = allowUserOverride.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}