using System.Text.RegularExpressions;

using ErrorOr;

using OpenClaw.Domain.Users.Errors;

namespace OpenClaw.Domain.Users.ValueObjects;

public sealed partial class UserEmail : IEquatable<UserEmail>
{
    public const int MaxLength = 256;

    public string Value { get; }

    public static ErrorOr<UserEmail> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UserErrors.EmptyEmail;
        }

        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed.Length > MaxLength)
        {
            return UserErrors.EmailTooLong;
        }

        if (!EmailRegex().IsMatch(trimmed))
        {
            return UserErrors.InvalidEmailFormat;
        }

        return new UserEmail(trimmed);
    }

    public bool Equals(UserEmail? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is UserEmail other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static implicit operator string(UserEmail email) => email.Value;

    private UserEmail(string value)
    {
        Value = value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
