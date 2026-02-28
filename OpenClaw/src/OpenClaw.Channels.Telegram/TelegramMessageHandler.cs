using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenClaw.Application.Skills;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Chat.Repositories;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Channels.Telegram;

public class TelegramMessageHandler(
    IServiceScopeFactory scopeFactory,
    ITelegramBotClient botClient,
    TelegramConversationMapper conversationMapper,
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramMessageHandler> logger)
{
    private readonly TelegramBotOptions _options = options.Value;

    // Built-in bot commands that are handled directly (not forwarded to the pipeline)
    private static readonly HashSet<string> BuiltInCommands = ["start", "new", "help", "skills"];

    public async Task HandleMessageAsync(Message message)
    {
        // Only handle text messages for now
        if (message.Text is null || message.From is null)
            return;

        var chatId = message.Chat.Id;
        var userId = message.From.Id;
        var username = message.From.Username ?? message.From.FirstName ?? userId.ToString();
        var text = message.Text.Trim();

        // Check whitelist
        if (_options.AllowedUserIds.Length > 0 && !_options.AllowedUserIds.Contains(userId))
        {
            logger.LogWarning("Unauthorized Telegram user {UserId} ({Username}) attempted to use the bot", userId, username);
            return;
        }

        logger.LogInformation("Telegram message from {Username} ({ChatId}): {Text}",
            username, chatId, text.Length > 100 ? text[..100] + "..." : text);

        try
        {
            // Handle built-in bot commands
            if (text.StartsWith('/'))
            {
                var commandText = text.Split(' ', '@')[0][1..].ToLowerInvariant();
                if (BuiltInCommands.Contains(commandText))
                {
                    await HandleBotCommandAsync(commandText, chatId, username);
                    return;
                }
            }

            // Show typing indicator
            await botClient.SendChatAction(chatId, ChatAction.Typing);

            // Process message through the agent pipeline
            await ProcessAgentMessageAsync(chatId, username, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Telegram message from {Username} ({ChatId})", username, chatId);
            await SendSafeAsync(chatId, "An error occurred while processing your message. Please try again.");
        }
    }

    private async Task HandleBotCommandAsync(string command, long chatId, string username)
    {
        switch (command)
        {
            case "start":
                await SendSafeAsync(chatId,
                    $"Welcome to OpenClaw\\! 🤖\n\n" +
                    $"I'm your AI Agent\\. Send me any message and I'll help you\\.\n\n" +
                    $"Use /help to see available commands\\.");
                break;

            case "new":
                conversationMapper.ResetConversation(chatId);
                await SendSafeAsync(chatId, "New conversation started\\. Send me a message\\!");
                break;

            case "help":
                await SendSafeAsync(chatId,
                    "*Available Commands:*\n\n" +
                    "/start \\- Welcome message\n" +
                    "/new \\- Start a new conversation\n" +
                    "/help \\- Show this help message\n" +
                    "/skills \\- List available skills\n" +
                    "/`skill\\_name` `args` \\- Execute a skill directly");
                break;

            case "skills":
                await HandleSkillsCommandAsync(chatId);
                break;
        }
    }

    private async Task HandleSkillsCommandAsync(long chatId)
    {
        using var scope = scopeFactory.CreateScope();
        var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();
        var skillSettingsService = scope.ServiceProvider.GetRequiredService<ISkillSettingsService>();

        var skills = skillRegistry.GetAllSkills();
        var sb = new StringBuilder("*Available Skills:*\n\n");

        foreach (var skill in skills)
        {
            var isEnabled = await skillSettingsService.IsEnabledAsync(skill.Name);
            var status = isEnabled ? "✅" : "❌";
            var name = EscapeMarkdownV2(skill.Name);
            var desc = EscapeMarkdownV2(skill.Description);
            sb.AppendLine($"{status} `{name}` \\- {desc}");
        }

        await SendSafeAsync(chatId, sb.ToString());
    }

    private async Task ProcessAgentMessageAsync(long chatId, string username, string text)
    {
        using var scope = scopeFactory.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IAgentPipeline>();
        var repository = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var slashCommandParser = scope.ServiceProvider.GetRequiredService<ISlashCommandParser>();
        var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();
        var skillSettingsService = scope.ServiceProvider.GetRequiredService<ISkillSettingsService>();

        // Get or create conversation
        var conversation = await conversationMapper.GetOrCreateConversationAsync(
            chatId, username, repository, uow, CancellationToken.None);

        // Load conversation history
        var history = conversation.Messages
            .Select(m => m.ToLlmMessage())
            .ToList();

        // Check for slash commands (skill invocation)
        if (slashCommandParser.TryParse(text, out var command))
        {
            var skill = skillRegistry.GetSkill(command!.SkillName);
            if (skill is null)
            {
                await SendSafeAsync(chatId, $"Skill '{EscapeMarkdownV2(command.SkillName)}' not found\\.");
                return;
            }

            if (!await skillSettingsService.IsEnabledAsync(command.SkillName))
            {
                await SendSafeAsync(chatId, $"Skill '{EscapeMarkdownV2(command.SkillName)}' is disabled\\.");
                return;
            }

            // Execute skill and inject result into history
            var jsonArgs = slashCommandParser.ConvertToJson(command, skill);
            var skillContext = new SkillContext(jsonArgs);
            var skillResult = await skill.ExecuteAsync(skillContext);

            if (!skillResult.IsSuccess)
            {
                await SendSafeAsync(chatId, $"Skill error: {EscapeMarkdownV2(skillResult.Error ?? "Unknown error")}");
                return;
            }

            var toolCallId = Guid.NewGuid().ToString();
            history.Add(new ChatMessage(
                ChatRole.Assistant,
                Content: null,
                ToolCalls: [new ToolCall(toolCallId, command.SkillName, jsonArgs)]));
            history.Add(new ChatMessage(ChatRole.Tool, skillResult.Output ?? "", toolCallId));
        }

        // Execute pipeline and collect full response
        var responseBuilder = new StringBuilder();
        await foreach (var evt in pipeline.ExecuteStreamAsync(text, history))
        {
            if (evt.Type == AgentStreamEventType.ContentDelta && evt.Content is not null)
            {
                responseBuilder.Append(evt.Content);
            }

            // Periodically send typing action for long responses
            if (responseBuilder.Length % 500 == 0 && responseBuilder.Length > 0)
            {
                await botClient.SendChatAction(chatId, ChatAction.Typing);
            }
        }

        var response = responseBuilder.ToString();

        if (string.IsNullOrWhiteSpace(response))
        {
            response = "I couldn't generate a response. Please try again.";
        }

        // Save conversation messages
        conversation.AddMessage(ChatRole.User, text);
        conversation.AddMessage(ChatRole.Assistant, response);
        await uow.SaveChangesAsync();

        // Convert and send response
        var formattedResponse = TelegramMarkdownConverter.ToTelegramMarkdownV2(response);
        var chunks = TelegramMarkdownConverter.SplitMessage(formattedResponse);

        foreach (var chunk in chunks)
        {
            await SendSafeAsync(chatId, chunk);
        }
    }

    private async Task SendSafeAsync(long chatId, string text)
    {
        try
        {
            await botClient.SendMessage(chatId, text, parseMode: ParseMode.MarkdownV2);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send MarkdownV2 message, falling back to plain text");
            try
            {
                // Fallback: strip markdown and send as plain text
                var plainText = text
                    .Replace("\\", "")
                    .Replace("*", "")
                    .Replace("_", "")
                    .Replace("`", "");
                await botClient.SendMessage(chatId, plainText);
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to send plain text message to {ChatId}", chatId);
            }
        }
    }

    private static string EscapeMarkdownV2(string text)
    {
        const string specialChars = @"_*[]()~`>#+-=|{}.!";
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (specialChars.Contains(c))
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
