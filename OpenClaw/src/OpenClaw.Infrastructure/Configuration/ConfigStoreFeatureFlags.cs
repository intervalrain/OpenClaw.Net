using OpenClaw.Contracts.Configuration;

namespace OpenClaw.Infrastructure.Configuration;

/// <summary>
/// Feature flags backed by IConfigStore.
/// Key format: FEATURE:{flagName} → "true" / "false"
/// Missing keys default to enabled (opt-out model).
/// </summary>
public class ConfigStoreFeatureFlags(IConfigStore configStore) : IFeatureFlags
{
    private const string Prefix = "FEATURE:";

    public bool IsEnabled(string featureName)
    {
        var value = configStore.Get($"{Prefix}{featureName}");
        // Default: enabled if not explicitly disabled
        return value is null || !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> IsEnabledAsync(string featureName, Guid? workspaceId = null, CancellationToken ct = default)
    {
        // Workspace-specific override: FEATURE:{flagName}:{workspaceId}
        if (workspaceId.HasValue)
        {
            var wsValue = configStore.Get($"{Prefix}{featureName}:{workspaceId.Value}");
            if (wsValue is not null)
                return Task.FromResult(!wsValue.Equals("false", StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(IsEnabled(featureName));
    }
}
