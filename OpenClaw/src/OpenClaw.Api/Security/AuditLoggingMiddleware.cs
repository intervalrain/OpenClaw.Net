using System.Security.Claims;
using OpenClaw.Domain.Audit.Entities;
using OpenClaw.Domain.Audit.Repositories;

namespace OpenClaw.Api.Security;

/// <summary>
/// Logs security-critical API requests for audit trail.
/// Persists to database and console log.
/// Covers: login, register, user management, config changes, cron job operations.
/// </summary>
public class AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
{
    private static readonly string[] AuditPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/register",
        "/api/v1/users",
        "/api/v1/user-management",
        "/api/v1/app-config",
        "/api/v1/cron-job",
        "/api/v1/model-provider",
        "/api/v1/user-model-provider",
        "/api/v1/channel-settings",
        "/api/v1/setup"
    ];

    /// <summary>
    /// Derives a human-readable action name from the HTTP method and path.
    /// </summary>
    private static string DeriveAction(string method, string path)
    {
        // Auth
        if (path.Contains("/auth/login")) return "auth.login";
        if (path.Contains("/auth/register")) return "auth.register";

        // User management
        if (path.Contains("/user-management"))
        {
            if (path.Contains("/ban")) return "user.ban";
            if (path.Contains("/unban")) return "user.unban";
            if (path.Contains("/approve")) return "user.approve";
            if (path.Contains("/role")) return "user.role_change";
            if (method == "DELETE") return "user.delete";
            return "user.manage";
        }

        // Config
        if (path.Contains("/app-config")) return $"config.{method.ToLower()}";
        if (path.Contains("/model-provider")) return $"provider.{method.ToLower()}";
        if (path.Contains("/user-model-provider")) return $"user_provider.{method.ToLower()}";
        if (path.Contains("/channel-settings")) return $"channel.{method.ToLower()}";

        // CronJob
        if (path.Contains("/cron-job"))
        {
            if (path.Contains("/execute")) return "cronjob.execute";
            if (method == "DELETE") return "cronjob.delete";
            return $"cronjob.{method.ToLower()}";
        }

        // Setup
        if (path.Contains("/setup")) return "setup";

        return $"{method.ToLower()}.{path.Split('/').LastOrDefault() ?? "unknown"}";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var method = context.Request.Method;

        // Only audit mutating requests and login
        var shouldAudit = AuditPaths.Any(p => path.StartsWith(p))
            && (method is "POST" or "PUT" or "DELETE" or "PATCH");

        if (!shouldAudit)
        {
            await next(context);
            return;
        }

        var userIdStr = context.User.FindFirstValue("id");
        var userId = Guid.TryParse(userIdStr, out var uid) ? uid : (Guid?)null;
        var userEmail = context.User.FindFirstValue(ClaimTypes.Email);
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var action = DeriveAction(method, path);

        await next(context);

        var statusCode = context.Response.StatusCode;

        // Console log (always)
        logger.LogInformation(
            "AUDIT | {Action} | {Method} {Path} | User: {UserId} ({Email}) | IP: {IP} | Status: {StatusCode}",
            action, method, path, userIdStr ?? "anonymous", userEmail ?? "unknown",
            ipAddress, statusCode);

        // Persist to database (fire-and-forget to avoid slowing down the response)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = context.RequestServices.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
                var entry = AuditLog.Create(userId, userEmail, action, method, path, statusCode, ipAddress, userAgent);
                await repository.AddAsync(entry);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist audit log for {Action}", action);
            }
        });
    }
}
