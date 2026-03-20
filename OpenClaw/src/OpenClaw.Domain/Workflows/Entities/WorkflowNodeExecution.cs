using Weda.Core.Domain;

namespace OpenClaw.Domain.Workflows.Entities;

/// <summary>
/// Represents the execution state of a single node in a workflow execution.
/// </summary>
public class WorkflowNodeExecution : Entity<Guid>
{
    public Guid WorkflowExecutionId { get; private set; }
    public string NodeId { get; private set; } = string.Empty;
    public NodeExecutionStatus Status { get; private set; }
    public string? InputJson { get; private set; }
    public string? OutputJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Navigation property
    public WorkflowExecution? WorkflowExecution { get; private set; }

    private WorkflowNodeExecution() : base(Guid.NewGuid()) { }

    public static WorkflowNodeExecution Create(Guid executionId, string nodeId)
    {
        return new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            WorkflowExecutionId = executionId,
            NodeId = nodeId,
            Status = NodeExecutionStatus.Pending
        };
    }

    public void Start(string? inputJson = null)
    {
        Status = NodeExecutionStatus.Running;
        InputJson = inputJson;
        StartedAt = DateTime.UtcNow;
    }

    public void SetWaitingForApproval()
    {
        Status = NodeExecutionStatus.WaitingForApproval;
    }

    public void Complete(string? outputJson = null)
    {
        Status = NodeExecutionStatus.Completed;
        OutputJson = outputJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = NodeExecutionStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void Skip()
    {
        Status = NodeExecutionStatus.Skipped;
        CompletedAt = DateTime.UtcNow;
    }
}
