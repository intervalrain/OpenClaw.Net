using System.Security.Claims;

namespace OpenClaw.Api.Security;

/// <summary>
/// Logs security-critical API requests for audit trail.
/// Covers: login, register, user management, config changes, cron job operations.
/// </summary>
public class AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
{
    // Paths that warrant audit logging
    private static readonly string[] AuditPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/register",
        "/api/v1/users",
        "/api/v1/user-management",
        "/api/v1/app-config",
        "/api/v1/cron-job",
        "/api/v1/setup"
    ];

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

        var userId = context.User.FindFirstValue("id") ?? "anonymous";
        var userEmail = context.User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();

        await next(context);

        var statusCode = context.Response.StatusCode;

        logger.LogInformation(
            "AUDIT | {Method} {Path} | User: {UserId} ({Email}) | IP: {IP} | UA: {UserAgent} | Status: {StatusCode}",
            method, path, userId, userEmail, ipAddress,
            userAgent.Length > 100 ? userAgent[..100] : userAgent,
            statusCode);
    }
}
