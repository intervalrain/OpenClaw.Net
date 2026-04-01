namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// Provides access to the current audit context via AsyncLocal storage.
/// Ensures trace context flows correctly across async calls.
/// </summary>
public static class AuditContextAccessor
{
    private static readonly AsyncLocal<IAuditContext?> _current = new();

    /// <summary>
    /// Gets or sets the current audit context for the async execution flow.
    /// </summary>
    public static IAuditContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
