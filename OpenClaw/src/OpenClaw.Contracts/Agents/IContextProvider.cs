namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Provides a block of context to be included in the system prompt.
/// Multiple providers are composed by SystemPromptAssembler.
///
/// Ref: Claude Code context.ts — memoized getSystemContext() + getUserContext(),
/// modular assembly from git status, memory, user context, system reminders.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Display order (lower = earlier in prompt). Default: 100.
    /// </summary>
    int Order => 100;

    /// <summary>
    /// Returns a context block to include in the system prompt, or null to skip.
    /// </summary>
    Task<string?> GetContextAsync(ContextProviderRequest request, CancellationToken ct = default);
}

/// <summary>
/// Input data available to context providers.
/// </summary>
public class ContextProviderRequest
{
    public string? Language { get; init; }
    public string? UserInput { get; init; }
    public Guid? UserId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public IReadOnlyList<string>? UserRoles { get; init; }
}
