namespace OpenClaw.Contracts.HierarchicalAgents;

public record AgentExecutionOptions
{
    public int MaxDepth { get; init; } = 5;
    public int MaxIterations { get; init; } = 10;
    public TimeSpan? Timeout { get; init; }
    public decimal? BudgetLimit { get; init; }
}
