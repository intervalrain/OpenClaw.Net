using Weda.Core.Domain;

namespace OpenClaw.Domain.HierarchicalAgents.Entities;

/// <summary>
/// Tracks the execution of an agent or a DAG workflow.
/// </summary>
public class AgentExecution : Entity<Guid>
{
    public Guid? ParentExecutionId { get; private set; }
    public string AgentName { get; private set; } = "";
    public string? TaskGraphJson { get; private set; }
    public string? NodeStatesJson { get; private set; }
    public AgentExecutionStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public decimal TotalTokensUsed { get; private set; }
    public string? ErrorMessage { get; private set; }

    private AgentExecution() : base(Guid.NewGuid()) { }

    public static AgentExecution Create(
        string agentName,
        string? taskGraphJson = null,
        Guid? parentExecutionId = null)
    {
        return new AgentExecution
        {
            Id = Guid.NewGuid(),
            AgentName = agentName,
            TaskGraphJson = taskGraphJson,
            ParentExecutionId = parentExecutionId,
            Status = AgentExecutionStatus.Pending,
            StartedAt = DateTime.UtcNow
        };
    }

    public void Start()
    {
        Status = AgentExecutionStatus.Running;
    }

    public void Complete(string? nodeStatesJson = null, decimal totalTokensUsed = 0)
    {
        Status = AgentExecutionStatus.Completed;
        NodeStatesJson = nodeStatesJson;
        TotalTokensUsed = totalTokensUsed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string? errorMessage = null)
    {
        Status = AgentExecutionStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = AgentExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }
}

public enum AgentExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
