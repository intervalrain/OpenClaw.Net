using Microsoft.EntityFrameworkCore;

using Weda.Core.Infrastructure.Persistence;
using ClawOS.Domain.Users.Entities;
using ClawOS.Domain.Users.Repositories;
using ClawOS.Domain.Users.ValueObjects;
using ClawOS.Infrastructure.Common.Persistence;

namespace ClawOS.Infrastructure.Users.Persistence;

public class UserRepository(AppDbContext dbContext)
    : GenericRepository<User, Guid, AppDbContext>(dbContext), IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var emailResult = UserEmail.Create(email);
        if (emailResult.IsError)
        {
            return null;
        }

        var userEmail = emailResult.Value;
        return await DbSet.FirstOrDefaultAsync(u => u.Email == userEmail, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var emailResult = UserEmail.Create(email);
        if (emailResult.IsError)
        {
            return false;
        }

        var userEmail = emailResult.Value;
        return await DbSet.AnyAsync(u => u.Email == userEmail, cancellationToken);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);
    }
}
