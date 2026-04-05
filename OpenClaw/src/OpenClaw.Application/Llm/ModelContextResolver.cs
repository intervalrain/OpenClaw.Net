using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;

namespace OpenClaw.Application.Llm;

/// <summary>
/// Resolves model context window via:
///   1. DB app-config (key: MODEL_CONTEXT:{modelName})
///   2. Ollama /api/show (if provider is ollama)
///   3. LiteLLM model_prices_and_context_window.json (remote, fuzzy match)
///   4. Conservative default (128K)
///
/// Results from steps 2-3 are written back to DB so subsequent calls are free.
/// </summary>
public class ModelContextResolver(
    IConfigStore configStore,
    IHttpClientFactory httpClientFactory,
    ILogger<ModelContextResolver> logger) : IModelContextResolver
{
    private const int DefaultContextWindow = 128_000;
    private const string ConfigPrefix = "MODEL_CONTEXT:";
    private const string LiteLlmUrl = "https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json";

    // In-memory cache to avoid repeated DB reads within the same process lifetime
    private static readonly ConcurrentDictionary<string, int> Cache = new(StringComparer.OrdinalIgnoreCase);

    // Lazy-loaded LiteLLM data (fetched once per process)
    private static readonly SemaphoreSlim LiteLlmLock = new(1, 1);
    private static Dictionary<string, JsonElement>? _liteLlmData;

    public async Task<int> ResolveAsync(string providerType, string modelName, string? providerUrl = null, CancellationToken ct = default)
    {
        var cacheKey = $"{providerType}:{modelName}";

        // 0. In-memory cache
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        // 1. DB app-config
        var configKey = $"{ConfigPrefix}{modelName}";
        var dbValue = configStore.Get(configKey);
        if (dbValue is not null && int.TryParse(dbValue, out var dbTokens))
        {
            Cache[cacheKey] = dbTokens;
            return dbTokens;
        }

        // 2. Provider-specific API query
        var apiResult = providerType.ToLowerInvariant() switch
        {
            "ollama" => await ResolveFromOllamaAsync(modelName, providerUrl, ct),
            _ => null
        };

        if (apiResult.HasValue)
        {
            await PersistAndCacheAsync(configKey, cacheKey, apiResult.Value, ct);
            return apiResult.Value;
        }

        // 3. LiteLLM remote JSON lookup (fuzzy match)
        var liteLlmResult = await ResolveFromLiteLlmAsync(modelName, ct);
        if (liteLlmResult.HasValue)
        {
            await PersistAndCacheAsync(configKey, cacheKey, liteLlmResult.Value, ct);
            return liteLlmResult.Value;
        }

        // 4. Default (don't persist — let it retry next time in case data becomes available)
        logger.LogWarning("Could not resolve context window for {Provider}:{Model}, using default {Default}",
            providerType, modelName, DefaultContextWindow);
        Cache[cacheKey] = DefaultContextWindow;
        return DefaultContextWindow;
    }

    private async Task<int?> ResolveFromOllamaAsync(string modelName, string? providerUrl, CancellationToken ct)
    {
        var baseUrl = (providerUrl ?? "http://localhost:11434").TrimEnd('/');
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/show",
                new { name = modelName },
                ct);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            // model_info.*.context_length or modelfile parameter num_ctx
            if (json.TryGetProperty("model_info", out var modelInfo))
            {
                foreach (var prop in modelInfo.EnumerateObject())
                {
                    if (prop.Name.EndsWith(".context_length") && prop.Value.TryGetInt32(out var ctx))
                    {
                        logger.LogInformation("Resolved context window for ollama:{Model} = {Tokens} via /api/show",
                            modelName, ctx);
                        return ctx;
                    }
                }
            }

            // Fallback: check parameters.num_ctx
            if (json.TryGetProperty("parameters", out var parameters))
            {
                var paramStr = parameters.GetString();
                if (paramStr is not null)
                {
                    foreach (var line in paramStr.Split('\n'))
                    {
                        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2 && parts[0] == "num_ctx" && int.TryParse(parts[1], out var numCtx))
                        {
                            logger.LogInformation("Resolved context window for ollama:{Model} = {Tokens} via num_ctx parameter",
                                modelName, numCtx);
                            return numCtx;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to query Ollama /api/show for {Model}", modelName);
            return null;
        }
    }

    private async Task<int?> ResolveFromLiteLlmAsync(string modelName, CancellationToken ct)
    {
        try
        {
            var data = await GetLiteLlmDataAsync(ct);
            if (data is null) return null;

            // Exact match first
            if (TryGetMaxInputTokens(data, modelName, out var tokens))
                return tokens;

            // Fuzzy: try common prefixes (e.g., "gpt-4o" matches "gpt-4o-2024-11-20")
            var normalizedName = modelName.ToLowerInvariant();
            foreach (var (key, value) in data)
            {
                var normalizedKey = key.ToLowerInvariant();
                // Match: key contains our model name, or our model name contains the key
                if ((normalizedKey.Contains(normalizedName) || normalizedName.Contains(normalizedKey))
                    && TryGetMaxInputTokens(key, value, out var fuzzyTokens))
                {
                    logger.LogInformation("Resolved context window for {Model} = {Tokens} via LiteLLM fuzzy match (key: {Key})",
                        modelName, fuzzyTokens, key);
                    return fuzzyTokens;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to resolve context window from LiteLLM for {Model}", modelName);
            return null;
        }
    }

    private static bool TryGetMaxInputTokens(Dictionary<string, JsonElement> data, string key, out int tokens)
    {
        tokens = 0;
        if (!data.TryGetValue(key, out var element)) return false;
        return TryGetMaxInputTokens(key, element, out tokens);
    }

    private static bool TryGetMaxInputTokens(string key, JsonElement element, out int tokens)
    {
        tokens = 0;
        if (element.ValueKind != JsonValueKind.Object) return false;

        if (element.TryGetProperty("max_input_tokens", out var maxInput) && maxInput.TryGetInt32(out tokens))
            return true;

        if (element.TryGetProperty("max_tokens", out var maxTokens) && maxTokens.TryGetInt32(out tokens))
            return true;

        return false;
    }

    private async Task<Dictionary<string, JsonElement>?> GetLiteLlmDataAsync(CancellationToken ct)
    {
        if (_liteLlmData is not null) return _liteLlmData;

        await LiteLlmLock.WaitAsync(ct);
        try
        {
            if (_liteLlmData is not null) return _liteLlmData;

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetAsync(LiteLlmUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch LiteLLM model data: {Status}", response.StatusCode);
                return null;
            }

            _liteLlmData = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(ct);
            logger.LogInformation("Loaded LiteLLM model data: {Count} entries", _liteLlmData?.Count ?? 0);
            return _liteLlmData;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch LiteLLM model data");
            return null;
        }
        finally
        {
            LiteLlmLock.Release();
        }
    }

    private async Task PersistAndCacheAsync(string configKey, string cacheKey, int tokens, CancellationToken ct)
    {
        Cache[cacheKey] = tokens;
        try
        {
            await configStore.SetAsync(configKey, tokens.ToString(), ct: ct);
            logger.LogInformation("Persisted context window to app-config: {Key} = {Tokens}", configKey, tokens);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist context window to app-config: {Key}", configKey);
        }
    }
}
