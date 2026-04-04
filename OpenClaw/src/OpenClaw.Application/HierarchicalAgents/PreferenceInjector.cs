using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Resolves user preferences and injects them into agent system prompts.
/// </summary>
public static partial class PreferenceInjector
{
    private const int MaxPreferenceValueLength = 500;

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    /// <summary>
    /// Appends user preferences to a system prompt if a UserId is available.
    /// Returns the original prompt if no user context or no preferences found.
    /// </summary>
    public static async Task<string> EnrichWithPreferencesAsync(
        string systemPrompt,
        AgentExecutionContext context,
        CancellationToken ct = default)
    {
        if (!context.UserId.HasValue)
            return systemPrompt;

        var prefRepo = context.Services.GetService<IUserPreferenceRepository>();
        if (prefRepo is null)
            return systemPrompt;

        var preferences = await prefRepo.GetAllByUserAsync(context.UserId.Value, ct);
        if (preferences.Count == 0)
            return systemPrompt;

        var prefLines = preferences
            .Select(p => $"- {SanitizeValue(p.Key)}: {SanitizeValue(p.Value ?? string.Empty)}")
            .ToList();

        var prefSection = $"""

            <user-preferences>
            The following are the current user's preferences. Respect these when generating responses:
            {string.Join("\n", prefLines)}
            </user-preferences>
            """;

        return systemPrompt + prefSection;
    }

    /// <summary>
    /// Strips XML/HTML-like tags and truncates excessively long values.
    /// </summary>
    internal static string SanitizeValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Strip XML/HTML-like tags
        var sanitized = HtmlTagPattern().Replace(value, string.Empty);

        // Truncate if excessively long
        if (sanitized.Length > MaxPreferenceValueLength)
            sanitized = sanitized[..MaxPreferenceValueLength] + "...";

        return sanitized;
    }
}
