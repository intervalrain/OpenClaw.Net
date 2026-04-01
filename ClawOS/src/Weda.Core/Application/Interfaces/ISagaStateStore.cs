using Weda.Core.Application.Sagas;

namespace Weda.Core.Application.Interfaces;

/// <summary>
/// Persistence for saga state
/// </summary>
public interface ISagaStateStore
{
    Task<SagaState<TData>?> GetAsync<TData>(string sagaId, CancellationToken ct = default) where TData : class;
    Task SaveAsync<TData>(SagaState<TData> state, CancellationToken ct = default) where TData : class;
    Task DeleteAsync(string sagaId, CancellationToken ct = default);
}