using Microsoft.EntityFrameworkCore;

using Weda.Core.Application.Security;

using Weda.Core.Application.Security.Models;
using OpenClaw.Domain.Users.Entities;

namespace OpenClaw.Infrastructure.Common.Persistence;

public class AppDbContextSeeder(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher)
{
    public async Task SeedAsync()
    {
        await SeedUsersAsync();
    }

    private async Task SeedUsersAsync()
    {
        // Check if users already exist
        if (await dbContext.Set<User>().AnyAsync())
        {
            return;
        }

        var users = new List<User>();

        // Create SuperAdmin user
        var adminResult = User.Create(
            email: "admin@weda.com",
            passwordHash: passwordHasher.HashPassword("1q2w3e"),
            name: "Administrator",
            roles: [Role.SuperAdmin, Role.Admin, Role.User],
            permissions: []);

        if (!adminResult.IsError)
        {
            users.Add(adminResult.Value);
        }

        // Create regular User
        var userResult = User.Create(
            email: "user@weda.com",
            passwordHash: passwordHasher.HashPassword("1q2w3e"),
            name: "Default User",
            roles: [Role.User],
            permissions: []);

        if (!userResult.IsError)
        {
            users.Add(userResult.Value);
        }

        if (users.Count > 0)
        {
            await dbContext.Set<User>().AddRangeAsync(users);
            await dbContext.SaveChangesAsync();
        }
    }
}
