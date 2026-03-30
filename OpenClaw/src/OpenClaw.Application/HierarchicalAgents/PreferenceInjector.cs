using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Resolves user preferences and injects them into agent system prompts.
/// </summary>
public static class PreferenceInjector
{
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
            .Select(p => $"- {p.Key}: {p.Value}")
            .ToList();

        var prefSection = $"""

            <user-preferences>
            The following are the current user's preferences. Respect these when generating responses:
            {string.Join("\n", prefLines)}
            </user-preferences>
            """;

        return systemPrompt + prefSection;
    }
}
