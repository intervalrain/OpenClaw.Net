using Weda.Core.Domain;

namespace OpenClaw.Domain.Configuration.Entities;

public class ModelProvider : Entity<Guid>
{
    public string Type { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public string? EncryptedApiKey { get; private set; }
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
        bool isActive = false)
    {
        return new ModelProvider
        {
            Type = type,
            Name = name,
            Url = url,
            ModelName = modelName,
            EncryptedApiKey = encryptedApiKey,
            IsActive = isActive,
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