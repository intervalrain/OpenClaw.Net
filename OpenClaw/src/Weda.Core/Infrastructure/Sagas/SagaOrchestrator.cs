using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Sagas;

namespace Weda.Core.Infrastructure.Sagas;

public class SagaOrchestrator(
    ILogger<SagaOrchestrator> logger,
    ISagaStateStore store)
{
    public async Task<ErrorOr<TData>> ExecuteAsync<TData>(
        ISaga<TData> saga,
        TData initialData,
        string? sagaId = null,
        CancellationToken ct = default) where TData : class
    {
        sagaId ??= Guid.NewGuid().ToString("N");

        var state = new SagaState<TData>
        {
            SagaId = sagaId,
            SagaType = saga.SagaType,
            Status = SagaStatus.Running,
            Data = initialData
        };

        await store.SaveAsync(state, ct);
        logger.LogInformation("Starting saga {SagaType} with ID {SagaId}", saga.SagaType, sagaId);

        try
        {
            // Execute steps
            for (int i = 0; i < saga.Steps.Count; i++)
            {
                var step = saga.Steps[i];
                state.CurrentStepIndex = i;

                logger.LogDebug("Executing step {StepName} ({Index}/{Total})",
                    step.Name, i + 1, saga.Steps.Count);

                var result = await step.ExecuteAsync(state.Data, ct);

                if (result.IsError)
                {
                    state.ErrorMessage = result.FirstError.Description;
                    return await CompensateAsync(saga, state, ct);
                }

                state.Data = result.Value;
                state.CompletedSteps.Add(step.Name);
                await store.SaveAsync(state, ct);
            }

            // Success
            state.Status = SagaStatus.Completed;
            state.CompletedAt = DateTime.UtcNow;
            await store.SaveAsync(state, ct);

            logger.LogInformation("Saga {SagaId} completed successfully", sagaId);
            return state.Data!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saga {SagaId} failed with exception", sagaId);
            state.ErrorMessage = ex.Message;
            return await CompensateAsync(saga, state, ct);
        }
    }

    private async Task<ErrorOr<TData>> CompensateAsync<TData>(
        ISaga<TData> saga,
        SagaState<TData> state,
        CancellationToken ct) where TData : class
    {
        state.Status = SagaStatus.Compensating;
        await store.SaveAsync(state, ct);

        logger.LogWarning("Compensating saga {SagaId}, rolling back {Count} steps",
            state.SagaId, state.CompletedSteps.Count);

        // Compensate in reverse order
        for (int i = state.CompletedSteps.Count - 1; i >= 0; i--)
        {
            var step = saga.Steps[i];

            try
            {
                logger.LogDebug("Compensating step {StepName}", step.Name);
                var result = await step.CompensateAsync(state.Data!, ct);

                if (result.IsError)
                {
                    logger.LogError("Compensation failed for step {StepName}: {Error}",
                        step.Name, result.FirstError.Description);
                }
                else
                {
                    state.Data = result.Value;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Compensation exception for step {StepName}", step.Name);
            }
        }

        state.Status = SagaStatus.Compensated;
        state.CompletedAt = DateTime.UtcNow;
        await store.SaveAsync(state, ct);

        return Error.Failure("Saga.Failed", state.ErrorMessage ?? "Saga execution failed");
    }
}