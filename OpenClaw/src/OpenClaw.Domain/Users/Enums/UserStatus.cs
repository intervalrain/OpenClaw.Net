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
    /// User is inactive (disabled by admin)
    /// </summary>
    Inactive,

    /// <summary>
    /// User is locked (too many failed login attempts)
    /// </summary>
    Locked
}
