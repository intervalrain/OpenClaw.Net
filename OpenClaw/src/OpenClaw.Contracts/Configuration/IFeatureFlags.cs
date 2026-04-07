namespace OpenClaw.Contracts.Configuration;

/// <summary>
/// Feature flag service for toggling experimental features.
/// Supports per-workspace overrides via app-config (key: FEATURE:{flagName}).
///
/// Ref: Claude Code feature() function — build-time dead code elimination
/// via GrowthBook. Our approach is runtime-based via DB config.
/// </summary>
public interface IFeatureFlags
{
    bool IsEnabled(string featureName);
    Task<bool> IsEnabledAsync(string featureName, Guid? workspaceId = null, CancellationToken ct = default);
}

/// <summary>
/// Known feature flag names.
/// </summary>
public static class Features
{
    public const string SubAgent = "SUB_AGENT";
    public const string PlanMode = "PLAN_MODE";
    public const string DeferredToolLoading = "DEFERRED_TOOL_LOADING";
    public const string StreamingTools = "STREAMING_TOOLS";
    public const string ContextCompression = "CONTEXT_COMPRESSION";
}
