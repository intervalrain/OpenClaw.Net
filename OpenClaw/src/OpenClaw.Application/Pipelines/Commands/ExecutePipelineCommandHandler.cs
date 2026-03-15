using ErrorOr;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Pipelines;
using OpenClaw.Contracts.Pipelines.Commands;
using OpenClaw.Contracts.Pipelines.Responses;
using OpenClaw.Contracts.Skills;

using Weda.Core.Application.Security;

namespace OpenClaw.Application.Pipelines.Commands;

public class ExecutePipelineCommandHandler(
    IServiceProvider serviceProvider,
    IEnumerable<ISkillPipeline> pipelines,
    IPipelineExecutionStore executionStore,
    ICurrentUserProvider currentUserProvider,
    ILogger<ExecutePipelineCommandHandler> logger) : IRequestHandler<ExecutePipelineCommand, ErrorOr<string>>
{
    public async ValueTask<ErrorOr<string>> Handle(ExecutePipelineCommand command, CancellationToken ct)
    {
        // Validate pipeline exists (using current scope's pipelines)
        var pipelineExists = pipelines.Any(p => p.Name == command.PipelineName);
        if (!pipelineExists)
        {
            return Error.NotFound("Pipeline.NotFound", $"Pipeline '{command.PipelineName}' not found");
        }

        // Capture userId from command or current user (before background task starts)
        var userId = command.UserId ?? GetCurrentUserId();

        var execution = await executionStore.CreateAsync(command.PipelineName, command.ArgsJson, ct);
        logger.LogInformation("Created pipeline execution {ExecutionId} for {PipelineName} (userId: {UserId})",
            execution.Id, command.PipelineName, userId);

        // Run pipeline in background with its own scope (fire and forget)
        _ = RunPipelineAsync(command.PipelineName, execution.Id, command.ArgsJson, userId, ct);

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

    private async Task RunPipelineAsync(string pipelineName, string executionId, string? argsJson, Guid? userId, CancellationToken ct)
    {
        // Create a new scope for background execution to avoid ObjectDisposedException
        using var scope = serviceProvider.CreateScope();
        var scopedPipelines = scope.ServiceProvider.GetRequiredService<IEnumerable<ISkillPipeline>>();
        var pipeline = scopedPipelines.FirstOrDefault(p => p.Name == pipelineName);

        if (pipeline == null)
        {
            logger.LogError("Pipeline {PipelineName} not found in background scope", pipelineName);
            await executionStore.UpdateStatusAsync(executionId, PipelineExecutionStatus.Failed, ct);
            return;
        }

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
                            .Select(c => new ProposedChangeInfo(
                                c.WorkItemId,
                                c.Title,
                                c.WorkItemType,
                                c.CurrentState,
                                c.ProposedState,
                                c.Reason,
                                c.RelatedCommits,
                                c.WorkItemUrl))
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
