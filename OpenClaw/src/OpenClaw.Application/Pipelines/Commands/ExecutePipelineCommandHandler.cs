using ErrorOr;

using Mediator;

using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Pipelines;
using OpenClaw.Contracts.Pipelines.Commands;
using OpenClaw.Contracts.Pipelines.Responses;
using OpenClaw.Contracts.Skills;

using Weda.Core.Application.Security;

namespace OpenClaw.Application.Pipelines.Commands;

public class ExecutePipelineCommandHandler(
    IEnumerable<ISkillPipeline> pipelines,
    IPipelineExecutionStore executionStore,
    ICurrentUserProvider currentUserProvider,
    ILogger<ExecutePipelineCommandHandler> logger) : IRequestHandler<ExecutePipelineCommand, ErrorOr<string>>
{
    public async ValueTask<ErrorOr<string>> Handle(ExecutePipelineCommand command, CancellationToken ct)
    {
        var pipeline = pipelines.FirstOrDefault(p => p.Name == command.PipelineName);
        if (pipeline == null)
        {
            return Error.NotFound("Pipeline.NotFound", $"Pipeline '{command.PipelineName}' not found");
        }

        // Capture userId from command or current user (before background task starts)
        var userId = command.UserId ?? GetCurrentUserId();

        var execution = await executionStore.CreateAsync(command.PipelineName, command.ArgsJson, ct);
        logger.LogInformation("Created pipeline execution {ExecutionId} for {PipelineName} (userId: {UserId})",
            execution.Id, command.PipelineName, userId);

        // Run pipeline in background (fire and forget)
        _ = RunPipelineAsync(pipeline, execution.Id, command.ArgsJson, userId, ct);

        return execution.Id;
    }

    private Guid? GetCurrentUserId()
    {
        try
        {
            return currentUserProvider.GetCurrentUser().Id;
        }
        catch
        {
            return null;
        }
    }

    private async Task RunPipelineAsync(ISkillPipeline pipeline, string executionId, string? argsJson, Guid? userId, CancellationToken ct)
    {
        try
        {
            await executionStore.UpdateStatusAsync(executionId, PipelineExecutionStatus.Running, ct);

            var context = new PipelineExecutionContext(userId, argsJson);
            var result = await pipeline.RunAsync(
                context,
                async approvalRequest =>
                {
                    var approvalInfo = new PipelineApprovalInfo(
                        approvalRequest.StepName,
                        approvalRequest.Description,
                        approvalRequest.ProposedChanges
                            .Select(c => new ProposedChangeInfo(c.WorkItemId, c.CurrentState, c.ProposedState, c.Reason))
                            .ToList());

                    await executionStore.SetPendingApprovalAsync(executionId, approvalInfo, ct);
                    await executionStore.UpdateStatusAsync(executionId, PipelineExecutionStatus.WaitingForApproval, ct);

                    logger.LogInformation("Pipeline {ExecutionId} waiting for approval", executionId);

                    return await executionStore.WaitForApprovalAsync(executionId, ct);
                },
                ct);

            await executionStore.SetResultAsync(executionId, result, ct);
            await executionStore.UpdateStatusAsync(
                executionId,
                result.IsSuccess ? PipelineExecutionStatus.Completed : PipelineExecutionStatus.Failed,
                ct);

            logger.LogInformation("Pipeline {ExecutionId} completed with success={IsSuccess}", executionId, result.IsSuccess);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pipeline {ExecutionId} failed with exception", executionId);
            await executionStore.UpdateStatusAsync(executionId, PipelineExecutionStatus.Failed, ct);
        }
    }
}
