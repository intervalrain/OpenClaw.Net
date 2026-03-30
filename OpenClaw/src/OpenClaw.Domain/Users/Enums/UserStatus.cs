namespace OpenClaw.Domain.Users.Enums;

public enum UserStatus
{
    /// <summary>
    /// User is pending approval by an admin
    /// </summary>
    Pending,

    /// <summary>
    /// User is active and can login
    /// </summary>
    Active,

    /// <summary>
    /// User is locked (too many failed login attempts, auto-recoverable)
    /// </summary>
    Locked,

    /// <summary>
    /// User is banned by admin (with reason, manual unban required)
    /// </summary>
    Banned
}
