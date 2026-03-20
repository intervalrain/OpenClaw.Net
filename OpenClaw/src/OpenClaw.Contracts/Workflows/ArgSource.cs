namespace OpenClaw.Contracts.Workflows;

/// <summary>
/// Defines the source of a skill argument value.
/// Values are resolved in priority order: FilledValue > ConfigKey > InputMapping > UserPreferenceKey
/// </summary>
public record ArgSource
{
    /// <summary>
    /// Priority 1: Explicit value set in the workflow definition.
    /// </summary>
    public string? FilledValue { get; init; }

    /// <summary>
    /// Priority 2: Key to look up in workflow-level variables.
    /// </summary>
    public string? ConfigKey { get; init; }

    /// <summary>
    /// Priority 3: Reference to upstream node output using "nodeId.jsonPath" syntax.
    /// Example: "skill_a.output" or "skill_a.data.items[0].id"
    /// </summary>
    public string? InputMapping { get; init; }

    /// <summary>
    /// Priority 4: Key to look up in user preferences (UserPreference entity).
    /// </summary>
    public string? UserPreferenceKey { get; init; }
}