using Weda.Core.Domain;

namespace OpenClaw.Domain.Auth.Entities;

public class PasswordResetToken : Entity
{
    public string Email { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsUsed { get; private set; }

    private PasswordResetToken() : base(Guid.NewGuid()) { }

    public static PasswordResetToken Create(string email, TimeSpan expiry)
    {
        return new PasswordResetToken
        {
            Email = email,
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.Add(expiry),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        };
    }

    public bool IsValid() => !IsUsed && DateTime.UtcNow <= ExpiresAt;

    public void MarkUsed() => IsUsed = true;
}
