using System.Text.Json;

namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// Abstraction over execution strategies.
/// CronJob/Trigger → IExecutionEngine → Simple loop (existing) or DAG executor (new)
/// </summary>
public interface IExecutionEngine
{
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct = default);
}

public record ExecutionRequest
{
    /// <summary>The content/prompt to execute.</summary>
    public required string Content { get; init; }

    /// <summary>System prompt to use.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>User ID for per-user LLM provider resolution.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Workspace ID for file operation scoping.</summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>Available tool names.</summary>
    public IReadOnlyList<string> ToolNames { get; init; } = [];

    /// <summary>Max LLM iterations for simple loop mode.</summary>
    public int MaxIterations { get; init; } = 10;
}

public record ExecutionResult
{
    public required bool IsSuccess { get; init; }
    public string? Output { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ToolCallsJson { get; init; }
    public decimal TotalTokensUsed { get; init; }

    public static ExecutionResult Success(string output, string? toolCallsJson = null) =>
        new() { IsSuccess = true, Output = output, ToolCallsJson = toolCallsJson };

    public static ExecutionResult Failure(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };
}
