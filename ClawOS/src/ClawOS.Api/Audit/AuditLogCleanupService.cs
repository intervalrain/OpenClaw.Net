using ClawOS.Domain.Audit.Repositories;

namespace ClawOS.Api.Audit;

/// <summary>
/// Background service that periodically cleans up old audit logs.
/// Runs daily, deletes logs older than the configured retention period.
/// </summary>
public class AuditLogCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditLogCleanupService> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 1 minute after startup before first run
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var retentionDays = configuration.GetValue("AuditLog:RetentionDays", 90);
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
                var deleted = await repository.DeleteOlderThanAsync(cutoff, stoppingToken);

                if (deleted > 0)
                    logger.LogInformation("Audit log cleanup: deleted {Count} entries older than {Days} days", deleted, retentionDays);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit log cleanup failed");
            }

            // Run once per day
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
