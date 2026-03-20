using System.Text.Json;
using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Contracts.Workflows.Queries;
using OpenClaw.Contracts.Workflows.Responses;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;

namespace OpenClaw.Application.Workflows.Queries;

public class GetWorkflowExecutionsQueryHandler(
    IWorkflowExecutionRepository repository) : IRequestHandler<GetWorkflowExecutionsQuery, ErrorOr<IReadOnlyList<WorkflowExecutionSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<WorkflowExecutionSummary>>> Handle(
        GetWorkflowExecutionsQuery request,
        CancellationToken ct)
    {
        IReadOnlyList<WorkflowExecution> executions;

        if (request.WorkflowId.HasValue)
        {
            executions = await repository.GetByWorkflowIdAsync(
                request.WorkflowId.Value,
                request.Limit,
                request.Offset,
                ct);
        }
        else
        {
            executions = await repository.GetRecentAsync(request.Limit, request.Offset, ct);
        }

        return executions.Select(e => new WorkflowExecutionSummary
        {
            Id = e.Id,
            Status = e.Status,
            StartedAt = e.StartedAt,
            CompletedAt = e.CompletedAt
        }).ToList();
    }
}

public class GetWorkflowExecutionQueryHandler(
    IWorkflowExecutionRepository executionRepo,
    IWorkflowDefinitionRepository definitionRepo,
    IWorkflowExecutionStore approvalStore) : IRequestHandler<GetWorkflowExecutionQuery, ErrorOr<WorkflowExecutionResponse>>
{
    public async ValueTask<ErrorOr<WorkflowExecutionResponse>> Handle(
        GetWorkflowExecutionQuery request,
        CancellationToken ct)
    {
        var execution = await executionRepo.GetByIdWithNodesAsync(request.ExecutionId, ct);
        if (execution is null)
        {
            return Error.NotFound($"Execution {request.ExecutionId} not found");
        }

        var workflow = await definitionRepo.GetByIdAsync(execution.WorkflowDefinitionId, ct);
        var graph = workflow is not null
            ? JsonSerializer.Deserialize<WorkflowGraph>(
                workflow.DefinitionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : null;

        var nodeMap = graph?.Nodes.ToDictionary(n => n.Id) ?? [];

        WorkflowApprovalInfo? pendingApproval = null;
        if (execution.Status == WorkflowExecutionStatus.WaitingForApproval)
        {
            var approval = await approvalStore.GetPendingApprovalAsync(execution.Id, ct);
            if (approval.HasValue)
            {
                pendingApproval = new WorkflowApprovalInfo
                {
                    NodeId = approval.Value.NodeId,
                    ApprovalName = approval.Value.ApprovalName,
                    Description = approval.Value.Description
                };
            }
        }

        return new WorkflowExecutionResponse
        {
            Id = execution.Id,
            WorkflowDefinitionId = execution.WorkflowDefinitionId,
            WorkflowName = workflow?.Name ?? "Unknown",
            UserId = execution.UserId,
            Status = execution.Status,
            Trigger = execution.Trigger,
            InputJson = execution.InputJson,
            OutputJson = execution.OutputJson,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            NodeExecutions = execution.NodeExecutions.Select(ne =>
            {
                nodeMap.TryGetValue(ne.NodeId, out var node);
                return new NodeExecutionResponse
                {
                    Id = ne.Id,
                    NodeId = ne.NodeId,
                    NodeLabel = node?.Label,
                    NodeType = node switch
                    {
                        StartNode => "start",
                        EndNode => "end",
                        SkillNode => "skill",
                        ApprovalNode => "approval",
                        _ => "unknown"
                    },
                    Status = ne.Status,
                    InputJson = ne.InputJson,
                    OutputJson = ne.OutputJson,
                    ErrorMessage = ne.ErrorMessage,
                    StartedAt = ne.StartedAt,
                    CompletedAt = ne.CompletedAt
                };
            }).ToList(),
            PendingApproval = pendingApproval
        };
    }
}

public class GetNodeExecutionQueryHandler(
    IWorkflowExecutionRepository repository) : IRequestHandler<GetNodeExecutionQuery, ErrorOr<NodeExecutionResponse>>
{
    public async ValueTask<ErrorOr<NodeExecutionResponse>> Handle(
        GetNodeExecutionQuery request,
        CancellationToken ct)
    {
        var execution = await repository.GetByIdWithNodesAsync(request.ExecutionId, ct);
        if (execution is null)
        {
            return Error.NotFound($"Execution {request.ExecutionId} not found");
        }

        var nodeExecution = execution.NodeExecutions.FirstOrDefault(ne => ne.NodeId == request.NodeId);
        if (nodeExecution is null)
        {
            return Error.NotFound($"Node {request.NodeId} not found in execution");
        }

        return new NodeExecutionResponse
        {
            Id = nodeExecution.Id,
            NodeId = nodeExecution.NodeId,
            NodeLabel = null,
            NodeType = "unknown",
            Status = nodeExecution.Status,
            InputJson = nodeExecution.InputJson,
            OutputJson = nodeExecution.OutputJson,
            ErrorMessage = nodeExecution.ErrorMessage,
            StartedAt = nodeExecution.StartedAt,
            CompletedAt = nodeExecution.CompletedAt
        };
    }
}
