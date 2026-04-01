namespace ClawOS.Contracts.Llm;

public interface ILlmProviderFactory
{
    Task<ILlmProvider> GetProviderAsync(CancellationToken ct = default);
    Task<ILlmProvider> GetProviderAsync(Guid userId, string? providerName = null, CancellationToken ct = default);
}