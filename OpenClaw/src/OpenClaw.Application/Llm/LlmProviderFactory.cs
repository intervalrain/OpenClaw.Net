using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Security;
using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;

namespace OpenClaw.Application.Llm;

public class LlmProviderFactory(
    IModelProviderRepository repository,
    IEncryptionService encryption,
    IServiceProvider sp) : ILlmProviderFactory
{
    public async Task<ILlmProvider> GetProviderAsync(CancellationToken ct = default)
    {
        var provider = await repository.GetActiveAsync(ct);

        return provider is null
            ? sp.GetRequiredKeyedService<ILlmProvider>("ollama")
            : CreateFromEntity(provider);
    }

    private ILlmProvider CreateFromEntity(ModelProvider provider)
    {
        string? apiKey = null;
        if (!string.IsNullOrEmpty(provider.EncryptedApiKey))
        {
            try
            {
                apiKey = encryption.Decrypt(provider.EncryptedApiKey);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Decryption failed (key mismatch), treat as if no API key is set
                apiKey = null;
            }
        }

        return provider.Type.ToLowerInvariant() switch
        {
            "ollama" => CreateProvider("ollama", provider.Url, provider.ModelName),
            "openai" or "anthropic" or "custom" when apiKey is not null
                => CreateProvider("openai", apiKey, provider.ModelName),
            "openai" or "anthropic" or "custom"
                => throw new InvalidOperationException($"API key is required for provider '{provider.Name}' but decryption failed or key is missing."),
            _ => throw new NotSupportedException($"Provider type '{provider.Type}' is not supported.")
        };
    }

    private ILlmProvider CreateProvider(string type, string urlOrApiKey, string model)
    {
        var factory = sp.GetRequiredKeyedService<Func<string, string, ILlmProvider>>(type);
        return factory(urlOrApiKey, model);
    }
}