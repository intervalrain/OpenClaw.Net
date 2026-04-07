using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents.ContextProviders;

/// <summary>
/// Injects language instruction into system prompt.
/// </summary>
public class LanguageProvider : IContextProvider
{
    public int Order => 10;

    public Task<string?> GetContextAsync(ContextProviderRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Language) || request.Language == "auto")
            return Task.FromResult<string?>(null);

        var instruction = request.Language switch
        {
            "zh-TW" => "Always response in Traditional Chinese (繁體中文).",
            "en" => "Always response in English.",
            "ja" => "Always response in Japanese.",
            "kr" => "Always response in Korean.",
            _ => null
        };

        return Task.FromResult(instruction);
    }
}
