namespace Weda.Core.Application.Sagas;

/// <summary>
/// Defines a saga with ordered steps
/// </summary>
/// <typeparam name="TData">The saga data type shared across all steps</typeparam>
public interface ISaga<TData> where TData : class
{
    /// <summary>
    /// Unique saga type name
    /// </summary>
    string SagaType { get; }

    /// <summary>
    /// Ordered list of steps to execute
    /// </summary>
    IReadOnlyList<ISagaStep<TData>> Steps { get;}
}