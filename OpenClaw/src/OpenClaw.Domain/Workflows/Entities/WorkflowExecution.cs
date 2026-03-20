using Weda.Core.Domain;

namespace OpenClaw.Domain.Workflows.Entities;

/// <summary>
/// Represents a single execution of a workflow.
/// </summary>
public class WorkflowExecution : Entity<Guid>
{
    public Guid WorkflowDefinitionId { get; private set; }
    public Guid? UserId { get; private set; }
    public WorkflowExecutionStatus Status { get; private set; }
    public ExecutionTrigger Trigger { get; private set; }
    public string? InputJson { get; private set; }
    public string? OutputJson { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Current node waiting for approval, if any.
    /// </summary>
    public string? PendingApprovalNodeId { get; private set; }

    // Navigation properties
    public WorkflowDefinition? WorkflowDefinition { get; private set; }

    private readonly List<WorkflowNodeExecution> _nodeExecutions = [];
    public IReadOnlyCollection<WorkflowNodeExecution> NodeExecutions => _nodeExecutions.AsReadOnly();

    private WorkflowExecution() : base(Guid.NewGuid()) { }

    public static WorkflowExecution Create(
        Guid workflowDefinitionId,
        Guid? userId,
        ExecutionTrigger trigger,
        string? inputJson = null)
    {
        return new WorkflowExecution
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = workflowDefinitionId,
            UserId = userId,
            Status = WorkflowExecutionStatus.Pending,
            Trigger = trigger,
            InputJson = inputJson,
            StartedAt = DateTime.UtcNow
        };
    }

    public void Start()
    {
        Status = WorkflowExecutionStatus.Running;
    }

    public void SetWaitingForApproval(string nodeId)
    {
        Status = WorkflowExecutionStatus.WaitingForApproval;
        PendingApprovalNodeId = nodeId;
    }

    public void ClearPendingApproval()
    {
        PendingApprovalNodeId = null;
        Status = WorkflowExecutionStatus.Running;
    }

    public void Complete(string? outputJson = null)
    {
        Status = WorkflowExecutionStatus.Completed;
        OutputJson = outputJson;
        CompletedAt = DateTime.UtcNow;
        PendingApprovalNodeId = null;
    }

    public void Fail(string? errorMessage = null)
    {
        Status = WorkflowExecutionStatus.Failed;
        OutputJson = errorMessage;
        CompletedAt = DateTime.UtcNow;
        PendingApprovalNodeId = null;
    }

    public void Reject()
    {
        Status = WorkflowExecutionStatus.Rejected;
        CompletedAt = DateTime.UtcNow;
        PendingApprovalNodeId = null;
    }

    public void Cancel()
    {
        Status = WorkflowExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        PendingApprovalNodeId = null;
    }

    public WorkflowNodeExecution AddNodeExecution(string nodeId)
    {
        var nodeExecution = WorkflowNodeExecution.Create(Id, nodeId);
        _nodeExecutions.Add(nodeExecution);
        return nodeExecution;
    }
}
