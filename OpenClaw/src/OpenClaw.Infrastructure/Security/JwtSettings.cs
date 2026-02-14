namespace OpenClaw.Infrastructure.Security;

public sealed class JwtSettings
{
    public const string Section = "JwtSettings";

    public string Secret { get; set; } = null!;

    public string Issuer { get; set; } = null!;

    public string Audience { get; set; } = null!;

    public int TokenExpirationInMinutes { get; set; }
}
