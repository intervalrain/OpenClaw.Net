namespace OpenClaw.Contracts.Llm;

/// <summary>
/// Resolves the context window size for a given model.
/// Fallback chain: DB app-config > Provider API / remote lookup > default.
/// Results are persisted to app-config so subsequent calls are free.
/// </summary>
public interface IModelContextResolver
{
    /// <summary>
    /// Returns the max context window tokens for the given model.
    /// </summary>
    Task<int> ResolveAsync(string providerType, string modelName, string? providerUrl = null, CancellationToken ct = default);
}
