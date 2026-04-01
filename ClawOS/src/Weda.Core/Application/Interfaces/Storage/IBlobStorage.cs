using ErrorOr;

namespace Weda.Core.Application.Interfaces.Storage;

public interface IBlobStorage
{
    Task<ErrorOr<BlobInfo>> PutAsync<T>(string key, T value, CancellationToken ct = default);
    
    Task<ErrorOr<T>> GetAsync<T>(string key, CancellationToken ct = default);
    
    Task<ErrorOr<Deleted>> DeleteAsync(string key, CancellationToken ct = default);

    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}