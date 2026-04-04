using System.Text.Json;

namespace OpenClaw.Contracts.HierarchicalAgents;

public record AgentResult
{
    public required JsonDocument Output { get; init; }
    public required AgentResultStatus Status { get; init; }
    public decimal TokensUsed { get; init; }
    public string? ErrorMessage { get; init; }

    public static AgentResult Success(JsonDocument output, decimal tokensUsed = 0) =>
        new() { Output = output, Status = AgentResultStatus.Success, TokensUsed = tokensUsed };

    public static AgentResult Failed(string error) =>
        new()
        {
            Output = JsonDocument.Parse("{}"),
            Status = AgentResultStatus.Failed,
            ErrorMessage = error
        };

    public static AgentResult Cancelled() =>
        new()
        {
            Output = JsonDocument.Parse("{}"),
            Status = AgentResultStatus.Cancelled
        };
}

public enum AgentResultStatus
{
    Success,
    Failed,
    Cancelled
}
