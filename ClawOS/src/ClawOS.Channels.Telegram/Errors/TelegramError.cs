using ErrorOr;

namespace ClawOS.Channels.Telegram.Errors;

public static class TelegramErrors
{
    public static Error UnauthorizedUser(long userId) =>
        Error.Unauthorized(
            code: "Telegram.UnauthorizedUser",
            description: $"User {userId} is not in the allowed users list");

    public static Error InvalidChatId(string chatId) =>
        Error.Validation(
            code: "Telegram.InvalidChatId",
            description: $"Invalid chat ID format: {chatId}");

    public static Error InvalidUserId(string userId) =>
        Error.Validation(
            code: "Telegram.InvalidUserId",
            description: $"Invalid user ID format: {userId}");

    public static Error EmptyMessage =>
        Error.Validation(
            code: "Telegram.EmptyMessage",
            description: "Message content is empty");

    public static Error SkillNotFound(string skillName) =>
        Error.NotFound(
            code: "Telegram.SkillNotFound",
            description: $"Skill '{skillName}' not found");

    public static Error SkillDisabled(string skillName) =>
        Error.Validation(
            code: "Telegram.SkillDisabled",
            description: $"Skill '{skillName}' is disabled");

    public static Error SkillExecutionFailed(string skillName, string? error) =>
        Error.Failure(
            code: "Telegram.SkillExecutionFailed",
            description: $"Skill '{skillName}' execution failed: {error ?? "Unknown error"}");

    public static Error MessageSendFailed(long chatId, string? reason) =>
        Error.Failure(
            code: "Telegram.MessageSendFailed",
            description: $"Failed to send message to chat {chatId}: {reason ?? "Unknown error"}");

    public static Error PipelineExecutionFailed(string? reason) =>
        Error.Failure(
            code: "Telegram.PipelineExecutionFailed",
            description: $"Agent pipeline execution failed: {reason ?? "Unknown error"}");
}