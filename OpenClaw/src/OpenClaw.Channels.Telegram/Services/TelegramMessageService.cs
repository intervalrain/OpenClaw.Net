using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenClaw.Application.Skills;
using OpenClaw.Channels.Telegram.Models;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Channels;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Chat.Repositories;

using Telegram.Bot;
using Telegram.Bot.Types.Enums;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Channels.Telegram.Services;

public interface ITelegramMessageService
{
    Task HandleMessageAsync(ChannelMessageReceivedEvent @event, CancellationToken cancellationToken = default);
}

public class TelegramMessageService(
    ILogger<TelegramMessageService> logger,
    ITelegramBotClient client,
    IAgentPipeline pipeline,
    TelegramConversationMapper mapper,
    IConversationRepository repository,
    ISlashCommandParser parser,
    IToolRegistry registry,
    IToolSettingsService settings,
    IOptions<TelegramBotOptions> options,
    IUnitOfWork uow) : ITelegramMessageService
{
    private readonly TelegramBotOptions _options = options.Value;
    private static readonly HashSet<string> _commands = ["start", "new", "help", "skills"];

    public async Task HandleMessageAsync(ChannelMessageReceivedEvent @event, CancellationToken cancellationToken = default)
    {
        var chatId = long.Parse(@event.ExternalChatId);
        var username = @event.ExternalUsername ?? @event.ExternalUserId;
        var text = @event.Content!.Trim();

        logger.LogInformation("Processing message from {Username} ({ChatId}): {Text}",
            username, chatId, text.Length > 100 ? text[..100] : text);

        if (text.StartsWith('/'))
        {
            var commandText = text.Split(' ', '@')[0][1..].ToLowerInvariant();
            if (_commands.Contains(commandText))
            {
                await HandleBotCommandAsync(commandText, chatId, username, cancellationToken);
                return;
            }
        }

        await client.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
        await ProcessAgentMessageAsync(chatId, username, text, cancellationToken);
    }

    private async Task HandleBotCommandAsync(string command, long chatId, string username, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case "start":
                await SendSafeAsync(chatId,
                    "Welcome to OpenClaw\\! 🤖\n\n" +
                    "I'm your AI Agent\\. Send me any message and I'll help you\\.\n\n" +
                    "Use /help to see available commands\\.", cancellationToken);
                break;

            case "new":
                mapper.ResetConversation(chatId);
                await SendSafeAsync(chatId, "New conversation started\\. Send me a message\\!", cancellationToken);
                break;

            case "help":
                await SendSafeAsync(chatId,
                    "*Available Commands:*\n\n" +
                    "/start \\- Welcome message\n" +
                    "/new \\- Start a new conversation\n" +
                    "/help \\- Show this help message\n" +
                    "/skills \\- List available skills\n" +
                    "/`skill\\_name` `args` \\- Execute a skill directly", cancellationToken);
                break;

            case "skills":
                await HandleSkillsCommandAsync(chatId, cancellationToken);
                break;
        }
    }

    private async Task HandleSkillsCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        var skills = registry.GetAllSkills();
        var sb = new StringBuilder("*Available Skills:*\n\n");

        foreach (var skill in skills)
        {
            var isEnabled = await settings.IsEnabledAsync(skill.Name, cancellationToken);
            var status = isEnabled ? "✅" : "❌";
            var name = TelegramMarkdownConverter.ToTelegramMarkdownV2(skill.Name);
            var desc = TelegramMarkdownConverter.ToTelegramMarkdownV2(skill.Description);

            sb.AppendLine($"{status} `{name}` \\- {desc}");
        }

        await SendSafeAsync(chatId, sb.ToString(), cancellationToken);
    }

    private async Task SendSafeAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            await client.SendMessage(chatId, text, parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send MarkdownV2 message, falling back to plain text");
            try
            {
                var plainText = text
                    .Replace("\\", "")
                    .Replace("*", "")
                    .Replace("_", "")
                    .Replace("`", "");

                await client.SendMessage(chatId, plainText, cancellationToken: cancellationToken);
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to send plain text message to {ChatId}", chatId);
            }
        }
    }

    private async Task ProcessAgentMessageAsync(long chatId, string username, string text, CancellationToken cancellationToken)
    {
        var conversation = await mapper.GetOrCreateConversationAsync(chatId, username, repository, uow, cancellationToken);

        var history = conversation.Messages.Select(m => m.ToLlmMessage()).ToList();

        if (parser.TryParse(text, out var command))
        {
            var skill = registry.GetSkill(command!.SkillName);
            if (skill is null)
            {
                await SendSafeAsync(chatId,
                    $"Skill '{TelegramMarkdownConverter.ToTelegramMarkdownV2(command.SkillName)}' not found\\.", cancellationToken);
                return;
            }

            if (!await settings.IsEnabledAsync(command.SkillName, cancellationToken))
            {
                await SendSafeAsync(chatId,
                    $"Skill '{TelegramMarkdownConverter.ToTelegramMarkdownV2(command.SkillName)}' is disabled\\.", cancellationToken);
                return;
            }

            var args = parser.ConvertToJson(command, skill);
            var context = new ToolContext(args);
            var result = await skill.ExecuteAsync(context, cancellationToken);

            var toolCallId = Guid.NewGuid().ToString();
            history.Add(new ChatMessage(
                ChatRole.Assistant,
                Content: null,
                ToolCalls: [new ToolCall(toolCallId, command.SkillName, args)]));
            history.Add(new ChatMessage(ChatRole.Tool, result.Output ?? "", toolCallId));
        }

        var responseBuilder = new StringBuilder();
        await foreach (var streamEvent in pipeline.ExecuteStreamAsync(text, history, ct: cancellationToken))
        {
            if (streamEvent.Type == AgentStreamEventType.ContentDelta && streamEvent.Content is not null)
            {
                responseBuilder.Append(streamEvent.Content);
            }

            if (responseBuilder.Length % 500 == 0 && responseBuilder.Length > 0)
            {
                await client.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
            }
        }

        var response = responseBuilder.ToString();
        if (string.IsNullOrWhiteSpace(response))
        {
            response = "I couldn't generate a response. Please try again";
        }

        conversation.AddMessage(ChatRole.User, text);
        conversation.AddMessage(ChatRole.Assistant, response);
        await uow.SaveChangesAsync(cancellationToken);

        var formattedResponse = TelegramMarkdownConverter.ToTelegramMarkdownV2(response);
        var chunks = TelegramMarkdownConverter.SplitMessage(formattedResponse);

        foreach (var chunk in chunks)
        {
            await SendSafeAsync(chatId, chunk, cancellationToken);
        }
    }
}
