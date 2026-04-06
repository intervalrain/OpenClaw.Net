namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Coordinates multiple worker agents for parallel task execution.
/// Ref: Claude Code coordinatorMode.ts — coordinator spawns workers,
/// synthesizes results, directs follow-up work.
///
/// Flow: Research workers (parallel) → Coordinator synthesis →
/// Implementation workers → Verification workers → Report.
/// </summary>
public interface ICoordinator
{
    /// <summary>
    /// Decomposes a task into parallel worker assignments and orchestrates execution.
    /// </summary>
    IAsyncEnumerable<AgentStreamEvent> CoordinateAsync(
        string task,
        CoordinatorOptions? options = null,
        Guid? userId = null,
        Guid? workspaceId = null,
        CancellationToken ct = default);
}

public class CoordinatorOptions
{
    public int MaxWorkers { get; set; } = 4;
    public int MaxIterationsPerWorker { get; set; } = 10;
    public string? SystemPrompt { get; set; }
}
