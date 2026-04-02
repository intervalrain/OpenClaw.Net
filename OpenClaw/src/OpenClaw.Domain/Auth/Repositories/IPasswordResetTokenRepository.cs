using OpenClaw.Domain.Auth.Entities;
using Weda.Core.Domain;

namespace OpenClaw.Domain.Auth.Repositories;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken, Guid>
{
    Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task InvalidateAllForEmailAsync(string email, CancellationToken ct = default);
}
