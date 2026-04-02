using OpenClaw.Domain.Auth.Entities;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Auth.Repositories;

public interface IEmailVerificationRepository : IRepository<EmailVerification, Guid>
{
    Task<EmailVerification?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task RemoveByEmailAsync(string email, CancellationToken cancellationToken = default);
}