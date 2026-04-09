namespace OpenClaw.Channels.Teams.Models;

public class TeamsBotOptions
{
    public const string SectionName = "Teams";

    /// <summary>Enable or disable the Teams channel adapter.</summary>
    public bool Enabled { get; set; }

    /// <summary>Azure Bot Registration App ID (client ID).</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>Azure Bot Registration App Password (client secret).</summary>
    public string AppPassword { get; set; } = string.Empty;

    /// <summary>
    /// Optional: restrict to a single Azure AD tenant.
    /// Leave empty for multi-tenant bots.
    /// </summary>
    public string? TenantId { get; set; }
}
