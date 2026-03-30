using Weda.Core.Domain;

namespace OpenClaw.Domain.Audit.Entities;

/// <summary>
/// Persistent audit log entry for security-critical operations.
/// Not user-scoped — only SuperAdmin can query.
/// </summary>
public class AuditLog : Entity<Guid>
{
    public Guid? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string HttpMethod { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime Timestamp { get; private set; }

    private AuditLog() : base(Guid.NewGuid()) { }

    public static AuditLog Create(
        Guid? userId,
        string? userEmail,
        string action,
        string httpMethod,
        string path,
        int statusCode,
        string? ipAddress,
        string? userAgent)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            HttpMethod = httpMethod,
            Path = path,
            StatusCode = statusCode,
            IpAddress = ipAddress,
            UserAgent = userAgent?.Length > 200 ? userAgent[..200] : userAgent,
            Timestamp = DateTime.UtcNow
        };
    }
}
