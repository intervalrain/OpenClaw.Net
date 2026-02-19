namespace OpenClaw.Contracts.Llm;

public interface ILlmProviderFactory
{
    Task<ILlmProvider> GetProviderAsync(CancellationToken ct = default);
}