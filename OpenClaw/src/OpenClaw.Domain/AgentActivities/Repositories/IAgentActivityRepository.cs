using OpenClaw.Domain.AgentActivities.Entities;

namespace OpenClaw.Domain.AgentActivities.Repositories;

public interface IAgentActivityRepository
{
    Task AddAsync(AgentActivity activity, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest activity per user (for current state snapshot).
    /// </summary>
    Task<IReadOnlyList<AgentActivity>> GetLatestPerUserAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets activities within a time range (for replay mode).
    /// </summary>
    Task<IReadOnlyList<AgentActivity>> GetByTimeRangeAsync(
        DateTime from, DateTime to, int limit = 1000, CancellationToken ct = default);

    /// <summary>
    /// Deletes activities older than the specified date (cleanup).
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
