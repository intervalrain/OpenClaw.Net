using System.Text.RegularExpressions;
using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents.Middlewares;

/// <summary>
/// Middleware that redacts sensitive information from agent output.
/// This acts as a second line of defense after skill-level blocking.
/// </summary>
public partial class SecretRedactionMiddleware : IAgentMiddleware
{
    private const string RedactedPlaceholder = "[REDACTED]";

    // Common API key patterns
    private static readonly Regex[] SecretPatterns =
    [
        // OpenAI API Key
        ApiKeyOpenAiRegex(),
        // Anthropic API Key
        ApiKeyAnthropicRegex(),
        // AWS Access Key
        AwsAccessKeyRegex(),
        // AWS Secret Key (base64-like, 40 chars)
        AwsSecretKeyRegex(),
        // GitHub Token
        GithubTokenRegex(),
        // Generic API Key patterns (key=value, key:value)
        GenericApiKeyRegex(),
        // Bearer tokens
        BearerTokenRegex(),
        // Base64-encoded secrets (long strings that look like secrets)
        Base64SecretRegex(),
        // Connection strings with passwords
        ConnectionStringPasswordRegex(),
        // Private keys
        PrivateKeyRegex()
    ];

    public async Task<string> InvokeAsync(AgentContext context, AgentDelegate next, CancellationToken ct = default)
    {
        var result = await next(context, ct);
        return RedactSecrets(result);
    }

    private static string RedactSecrets(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        foreach (var pattern in SecretPatterns)
        {
            content = pattern.Replace(content, match =>
            {
                // Preserve the key name but redact the value
                var groups = match.Groups;
                if (groups.Count > 1 && groups[1].Success)
                {
                    return $"{groups[1].Value}{RedactedPlaceholder}";
                }
                return RedactedPlaceholder;
            });
        }

        return content;
    }

    // OpenAI: sk-...
    [GeneratedRegex(@"sk-[a-zA-Z0-9]{20,}", RegexOptions.Compiled)]
    private static partial Regex ApiKeyOpenAiRegex();

    // Anthropic: sk-ant-...
    [GeneratedRegex(@"sk-ant-[a-zA-Z0-9\-]{20,}", RegexOptions.Compiled)]
    private static partial Regex ApiKeyAnthropicRegex();

    // AWS Access Key: AKIA...
    [GeneratedRegex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)]
    private static partial Regex AwsAccessKeyRegex();

    // AWS Secret Key: 40 char base64-like
    [GeneratedRegex(@"(?<=aws_secret_access_key\s*[=:]\s*)[A-Za-z0-9/+=]{40}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex AwsSecretKeyRegex();

    // GitHub tokens
    [GeneratedRegex(@"gh[pousr]_[A-Za-z0-9_]{36,}", RegexOptions.Compiled)]
    private static partial Regex GithubTokenRegex();

    // Generic patterns: API_KEY=xxx, apiKey: "xxx", etc.
    [GeneratedRegex(@"((?:api[_-]?key|secret|token|password|credential|auth)\s*[=:]\s*[""']?)([a-zA-Z0-9\-_./+=]{16,})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GenericApiKeyRegex();

    // Bearer tokens
    [GeneratedRegex(@"(Bearer\s+)[A-Za-z0-9\-_./+=]{20,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    // Long base64-like strings that might be secrets
    [GeneratedRegex(@"(?<![a-zA-Z0-9])[A-Za-z0-9+/]{64,}={0,2}(?![a-zA-Z0-9])", RegexOptions.Compiled)]
    private static partial Regex Base64SecretRegex();

    // Connection string passwords
    [GeneratedRegex(@"((?:password|pwd)\s*=\s*)([^;""'\s]{8,})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPasswordRegex();

    // Private key markers
    [GeneratedRegex(@"-----BEGIN\s+(?:RSA\s+)?PRIVATE\s+KEY-----[\s\S]*?-----END\s+(?:RSA\s+)?PRIVATE\s+KEY-----", RegexOptions.Compiled)]
    private static partial Regex PrivateKeyRegex();
}