namespace Weda.Core.Application.Sagas;

/// <summary>
/// Represented the persisted state of a saga execution
/// </summary>
/// <typeparam name="TData"></typeparam>
public class SagaState<TData> where TData : class
{
    public required string SagaId { get; init; }
    public required string SagaType { get; init; }
    public SagaStatus Status { get; set; } = SagaStatus.Pending;
    public int CurrentStepIndex { get; set; } = 0;
    public TData? Data { get; set; }
    public List<string> CompletedSteps { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}