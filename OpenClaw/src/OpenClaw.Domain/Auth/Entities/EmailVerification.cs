using Weda.Core.Domain;

namespace OpenClaw.Domain.Auth.Entities;

public class EmailVerification : Entity
{
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int Attempts { get; private set; }

    private EmailVerification(): base(Guid.NewGuid()) { }
    
    public static EmailVerification Create(
        string email,
        string name,
        string passwordHash,
        string code,
        TimeSpan expiry)
    {
        return new EmailVerification
        {
            Email = email,
            Name = name,
            PasswordHash = passwordHash,
            Code = code,
            ExpiresAt = DateTime.UtcNow.Add(expiry),
            CreatedAt = DateTime.UtcNow,
            Attempts = 0 
        };
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    public bool HasExceededMaxAttempts(int times = 3) => Attempts >= times;

    public bool TryVerify(string code)
    {
        Attempts++;
        return Code == code && !IsExpired();
    }

    public void ResetCode(string newCode, TimeSpan expiry)
    {
        Code = newCode;
        ExpiresAt = DateTime.UtcNow.Add(expiry);
        Attempts = 0;
    }
}