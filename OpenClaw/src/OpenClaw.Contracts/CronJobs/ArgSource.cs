namespace OpenClaw.Contracts.CronJobs;

/// <summary>
/// Defines the source of a tool argument value.
/// Values are resolved in priority order: FilledValue > ConfigKey > UserPreferenceKey
/// </summary>
public record ArgSource
{
    /// <summary>
    /// Priority 1: Explicit value set in the cron job definition.
    /// </summary>
    public string? FilledValue { get; init; }

    /// <summary>
    /// Priority 2: Key to look up in cron job-level variables.
    /// </summary>
    public string? ConfigKey { get; init; }

    /// <summary>
    /// Priority 3: Key to look up in user preferences (UserPreference entity).
    /// </summary>
    public string? UserPreferenceKey { get; init; }
}
