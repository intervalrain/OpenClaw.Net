using ErrorOr;

namespace Weda.Core.Application.Sagas;

/// <summary>
/// Represents a single step in a saga
/// </summary>
/// <typeparam name="TData"></typeparam>
public interface ISagaStep<TData> where TData : class
{
    /// <summary>
    /// Step name for logging and tracking
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execute the step
    /// </summary>
    Task<ErrorOr<TData>> ExecuteAsync(TData data, CancellationToken ct = default);

    /// <summary>
    /// Compensate (rollback) the step
    /// </summary>
    Task<ErrorOr<TData>> CompensateAsync(TData data, CancellationToken ct = default);
}