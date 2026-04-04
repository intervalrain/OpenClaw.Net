using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenClaw.Infrastructure.Common.Persistence;

/// <summary>
/// Handles database migration, corrupted state recovery, and seeding.
/// Extracted from Program.cs for cleaner startup.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            await DetectAndRepairCorruptedStateAsync(dbContext, logger);
            await ApplyPendingMigrationsAsync(dbContext, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed");
            throw;
        }

        var seeder = scope.ServiceProvider.GetRequiredService<AppDbContextSeeder>();
        await seeder.SeedAsync();

        await EnsurePersonalWorkspacesAsync(dbContext, logger);
        await MigrateWorkspaceDirectoriesAsync(dbContext, logger);
    }

    /// <summary>
    /// Ensure every user has a personal workspace. Legacy users created before
    /// the workspace feature won't have one.
    /// </summary>
    private static async Task EnsurePersonalWorkspacesAsync(AppDbContext dbContext, ILogger logger)
    {
        var usersWithoutWorkspace = await dbContext.Set<Domain.Users.Entities.User>()
            .Where(u => !dbContext.Set<Domain.Workspaces.Entities.Workspace>()
                .Any(w => w.OwnerUserId == u.Id && w.IsPersonal))
            .ToListAsync();

        foreach (var user in usersWithoutWorkspace)
        {
            var ws = Domain.Workspaces.Entities.Workspace.CreatePersonal(user.Id, user.Name);
            await dbContext.Set<Domain.Workspaces.Entities.Workspace>().AddAsync(ws);
            logger.LogInformation("Created personal workspace for legacy user {UserId} ({Name})", user.Id, user.Name);
        }

        if (usersWithoutWorkspace.Count > 0)
            await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// One-time migration: rename workspace directories from {userId} to {workspaceId}.
    /// Idempotent — skips if directory already matches workspaceId.
    /// </summary>
    private static async Task MigrateWorkspaceDirectoriesAsync(AppDbContext dbContext, ILogger logger)
    {
        var basePath = Environment.GetEnvironmentVariable("OPENCLAW_WORKSPACE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "workspace");

        if (!Directory.Exists(basePath)) return;

        var personalWorkspaces = await dbContext.Set<Domain.Workspaces.Entities.Workspace>()
            .Where(w => w.IsPersonal)
            .Select(w => new { w.Id, w.OwnerUserId })
            .ToListAsync();

        foreach (var ws in personalWorkspaces)
        {
            var userDir = Path.Combine(basePath, ws.OwnerUserId.ToString());
            var wsDir = Path.Combine(basePath, ws.Id.ToString());

            // Already migrated or no user dir exists
            if (Directory.Exists(wsDir) || !Directory.Exists(userDir))
                continue;

            try
            {
                Directory.Move(userDir, wsDir);
                logger.LogInformation("Migrated workspace directory: {UserId} -> {WorkspaceId}", ws.OwnerUserId, ws.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to migrate workspace directory for user {UserId}", ws.OwnerUserId);
            }
        }
    }

    /// <summary>
    /// Detect corrupted state: __EFMigrationsHistory says InitialCreate ran but tables are missing.
    /// This happens when a legacy DB (created via EnsureCreated) was incorrectly baselined.
    /// Fix: drop everything and let Migrate() rebuild from scratch.
    /// </summary>
    private static async Task DetectAndRepairCorruptedStateAsync(AppDbContext dbContext, ILogger logger)
    {
        var conn = dbContext.Database.GetDbConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_name = '__EFMigrationsHistory'
            )
        """;
        var historyExists = (bool)(await cmd.ExecuteScalarAsync())!;

        if (!historyExists) return;

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'cron_jobs')";
        var schemaValid = (bool)(await cmd.ExecuteScalarAsync())!;

        if (schemaValid) return;

        logger.LogWarning("Corrupted migration state detected (history exists but schema incomplete). Rebuilding database...");
        await conn.CloseAsync();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        await conn.OpenAsync();

        using var seedCmd = conn.CreateCommand();
        seedCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" varchar(150) NOT NULL PRIMARY KEY,
                "ProductVersion" varchar(32) NOT NULL
            );
        """;
        await seedCmd.ExecuteNonQueryAsync();

        foreach (var migration in dbContext.Database.GetMigrations())
        {
            seedCmd.CommandText = $"""INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('{migration}', '10.0.0') ON CONFLICT DO NOTHING""";
            await seedCmd.ExecuteNonQueryAsync();
        }

        logger.LogInformation("Database rebuilt and migration history seeded");
    }

    private static async Task ApplyPendingMigrationsAsync(AppDbContext dbContext, ILogger logger)
    {
        var pending = dbContext.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
        }

        dbContext.Database.Migrate();
        logger.LogInformation("Database migration completed");
    }
}
