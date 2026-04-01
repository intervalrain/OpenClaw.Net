using ClawOS.Domain.Audit.Entities;

namespace ClawOS.Domain.Audit.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> QueryAsync(
        Guid? userId = null,
        string? action = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
