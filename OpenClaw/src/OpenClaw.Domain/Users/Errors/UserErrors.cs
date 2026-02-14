using ErrorOr;

namespace OpenClaw.Domain.Users.Errors;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound(
        code: "User.NotFound",
        description: "The user with the specified ID was not found.");

    public static readonly Error InvalidCredentials = Error.Unauthorized(
        code: "User.InvalidCredentials",
        description: "Invalid email or password.");

    public static readonly Error AccountInactive = Error.Unauthorized(
        code: "User.AccountInactive",
        description: "This account is inactive or locked.");

    public static readonly Error EmailAlreadyExists = Error.Conflict(
        code: "User.EmailAlreadyExists",
        description: "A user with this email already exists.");

    public static readonly Error EmptyEmail = Error.Validation(
        code: "User.EmptyEmail",
        description: "Email cannot be empty.");

    public static readonly Error InvalidEmailFormat = Error.Validation(
        code: "User.InvalidEmailFormat",
        description: "The email format is invalid.");

    public static readonly Error EmailTooLong = Error.Validation(
        code: "User.EmailTooLong",
        description: "Email cannot exceed 256 characters.");

    public static readonly Error EmptyName = Error.Validation(
        code: "User.EmptyName",
        description: "Name cannot be empty.");

    public static readonly Error NameTooLong = Error.Validation(
        code: "User.NameTooLong",
        description: "Name cannot exceed 100 characters.");

    public static readonly Error EmptyPassword = Error.Validation(
        code: "User.EmptyPassword",
        description: "Password cannot be empty.");

    public static readonly Error OnlySuperAdminCanChangeRoles = Error.Unauthorized(
        code: "User.OnlySuperAdminCanChangeRoles",
        description: "Only SuperAdmin can change user roles.");

    public static readonly Error OnlySuperAdminCanChangePermissions = Error.Unauthorized(
        code: "User.OnlySuperAdminCanChangePermissions",
        description: "Only SuperAdmin can change user permissions.");

    public static readonly Error CannotDeleteSelf = Error.Validation(
        code: "User.CannotDeleteSelf",
        description: "You cannot delete your own account.");

    public static readonly Error CannotRemoveOwnSuperAdmin = Error.Validation(
        code: "User.CannotRemoveOwnSuperAdmin",
        description: "You cannot remove your own SuperAdmin role.");

    public static readonly Error AccountNotActive = Error.Unauthorized(
        code: "User.AccountNotActive",
        description: "This account is not active.");

    public static readonly Error DuplicateEmail = Error.Conflict(
        code: "User.DuplicateEmail",
        description: "A user with this email address already exists.");

    public static readonly Error CannotDeleteSuperAdmin = Error.Forbidden(
        code: "User.CannotDeleteSuperAdmin",
        description: "Only SuperAdmin can delete another SuperAdmin.");
}
