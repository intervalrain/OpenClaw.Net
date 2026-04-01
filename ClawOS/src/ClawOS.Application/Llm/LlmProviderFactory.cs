using Microsoft.Extensions.DependencyInjection;

using ClawOS.Contracts.Llm;
using ClawOS.Contracts.Security;
using ClawOS.Domain.Configuration.Entities;
using ClawOS.Domain.Configuration.Repositories;

namespace ClawOS.Application.Llm;

public class LlmProviderFactory(
    IModelProviderRepository repository,
    IUserModelProviderRepository userRepository,
    IEncryptionService encryption,
    IServiceProvider sp) : ILlmProviderFactory
{
    public async Task<ILlmProvider> GetProviderAsync(CancellationToken ct = default)
    {
        var provider = await repository.GetActiveAsync(ct);

        return provider is null
            ? sp.GetRequiredKeyedService<ILlmProvider>("ollama")
            : CreateFromGlobalProvider(provider);
    }

    public async Task<ILlmProvider> GetProviderAsync(Guid userId, string? providerName = null, CancellationToken ct = default)
    {
        // 1. Try user's specific provider by name
        if (providerName is not null)
        {
            var byName = await userRepository.GetByNameAsync(userId, providerName, ct);
            if (byName is not null)
                return await CreateFromUserProvider(byName, ct);
        }

        // 2. Try user's default provider
        var userDefault = await userRepository.GetDefaultAsync(userId, ct);
        if (userDefault is not null)
            return await CreateFromUserProvider(userDefault, ct);

        // 3. Fallback to global active provider
        return await GetProviderAsync(ct);
    }

    private async Task<ILlmProvider> CreateFromUserProvider(UserModelProvider userProvider, CancellationToken ct)
    {
        // If referencing a global provider, use its credentials
        if (userProvider.GlobalModelProviderId.HasValue)
        {
            var global = await repository.GetByIdAsync(userProvider.GlobalModelProviderId.Value, ct);
            if (global is not null)
                return CreateFromGlobalProvider(global);
        }

        // Custom user provider with own credentials
        return CreateFromProviderConfig(
            userProvider.Type,
            userProvider.Name,
            userProvider.Url,
            userProvider.ModelName,
            userProvider.EncryptedApiKey);
    }

    private ILlmProvider CreateFromGlobalProvider(ModelProvider provider)
    {
        return CreateFromProviderConfig(
            provider.Type,
            provider.Name,
            provider.Url,
            provider.ModelName,
            provider.EncryptedApiKey);
    }

    private ILlmProvider CreateFromProviderConfig(
        string type, string name, string url, string modelName, string? encryptedApiKey)
    {
        string? apiKey = null;
        if (!string.IsNullOrEmpty(encryptedApiKey))
        {
            try
            {
                apiKey = encryption.Decrypt(encryptedApiKey);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                apiKey = null;
            }
        }

        return type.ToLowerInvariant() switch
        {
            "ollama" => CreateProvider("ollama", url, modelName),
            "openai" or "anthropic" or "custom" when apiKey is not null
                => CreateProvider("openai", apiKey, modelName),
            "openai" or "anthropic" or "custom"
                => throw new InvalidOperationException($"API key is required for provider '{name}' but decryption failed or key is missing."),
            _ => throw new NotSupportedException($"Provider type '{type}' is not supported.")
        };
    }

    private ILlmProvider CreateProvider(string type, string urlOrApiKey, string model)
    {
        var factory = sp.GetRequiredKeyedService<Func<string, string, ILlmProvider>>(type);
        return factory(urlOrApiKey, model);
    }
}