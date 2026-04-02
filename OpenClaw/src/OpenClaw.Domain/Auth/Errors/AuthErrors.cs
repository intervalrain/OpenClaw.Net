using ErrorOr;

namespace OpenClaw.Domain.Auth.Errors;

public static class AuthErrors
{
    public static readonly Error PasswordRequired = Error.Validation(
        code: "Auth.PasswordRequired",
        description: "Password is required.");

    public static readonly Error PasswordTooShort = Error.Validation(
        code: "Auth.PasswordTooShort",
        description: "Password must be at least 8 characters long.");

    public static readonly Error PasswordMissingUppercase = Error.Validation(
        code: "Auth.PasswordMissingUppercase",
        description: "Password must contain at least one uppercase letter.");

    public static readonly Error PasswordMissingLowercase = Error.Validation(
        code: "Auth.PasswordMissingLowercase",
        description: "Password must contain at least one lowercase letter.");

    public static readonly Error PasswordMissingDigit = Error.Validation(
        code: "Auth.PasswordMissingDigit",
        description: "Password must contain at least one digit.");

    public static readonly Error PasswordMissingSpecialChar = Error.Validation(
        code: "Auth.PasswordMissingSpecialChar",
        description: "Password must contain at least one special character.");

    public static readonly Error VerificationCodeInvalid = Error.Validation(
        code: "Auth.VerificationCodeInvalid",
        description: "Invalid or expired verification code.");

    public static readonly Error VerificationNotFound = Error.NotFound(
        code: "Auth.VerificationNotFound",
        description: "No pending verification found for this email.");

    public static readonly Error VerificationMaxAttempts = Error.Validation(
        code: "Auth.VerificationMaxAttempts",
        description: "Too many verification attempts. Please request a new code.");
}