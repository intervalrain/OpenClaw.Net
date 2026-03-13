using ErrorOr;

namespace OpenClaw.Domain.Users.Errors;

public static class UserPreferenceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        code: "UserPreference.NotFound",
        description: "The preference with the specified key was not found.");

    public static readonly Error KeyRequired = Error.Validation(
        code: "UserPreference.KeyRequired",
        description: "Preference key is required.");
}
