namespace OpenClaw.Domain.Users.ValueObjects;

public sealed class PasswordHash : IEquatable<PasswordHash>
{
    public string Value { get; }

    private PasswordHash(string value)
    {
        Value = value;
    }

    public static PasswordHash Create(string hashedValue)
    {
        return new PasswordHash(hashedValue);
    }

    public bool Equals(PasswordHash? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is PasswordHash other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator string(PasswordHash passwordHash) => passwordHash.Value;
}
