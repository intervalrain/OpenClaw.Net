using System.Text;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

using OpenClaw.Application.Channels;
using OpenClaw.Channels.Teams.Adapters;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Channels;
using OpenClaw.Contracts.Llm;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Channels.Teams.Services;

public interface ITeamsMessageService
{
    Task HandleMessageAsync(ChannelMessageReceivedEvent @event, CancellationToken ct = default);
}

public class TeamsMessageService(
    ILogger<TeamsMessageService> logger,
    TeamsChannelAdapter adapter,
    IAgentPipeline pipeline,
    TeamsConversationMapper mapper,
    ChannelLinkService linkService,
    IConversationRepository repository,
    IUnitOfWork uow) : ITeamsMessageService
{
    private static readonly HashSet<string> _commands = ["start", "new", "help", "link", "unlink"];

    public async Task HandleMessageAsync(ChannelMessageReceivedEvent @event, CancellationToken ct = default)
    {
        var conversationId = @event.ExternalChatId;
        var username = @event.ExternalUsername ?? @event.ExternalUserId;
        var text = @event.Content!.Trim();

        logger.LogInformation("Processing Teams message from {Username} ({ConversationId}): {Text}",
            username, conversationId, text.Length > 100 ? text[..100] : text);

        // Handle bot commands
        if (text.StartsWith('/'))
        {
            var commandText = text.Split(' ')[0][1..].ToLowerInvariant();
            if (_commands.Contains(commandText))
            {
                await HandleBotCommandAsync(commandText, conversationId, @event.ExternalUserId, username, ct);
                return;
            }
        }

        await ProcessAgentMessageAsync(conversationId, @event.ExternalUserId, username, text, ct);
    }

    private async Task HandleBotCommandAsync(
        string command, string conversationId, string externalUserId, string username, CancellationToken ct)
    {
        switch (command)
        {
            case "start":
                await SendAsync(conversationId,
                    "Welcome to OpenClaw! I'm your AI Agent. Send me any message and I'll help you.\n\nUse /help to see available commands.", ct);
                break;

            case "new":
                mapper.ResetConversation(conversationId);
                await SendAsync(conversationId, "New conversation started. Send me a message!", ct);
                break;

            case "link":
                var code = linkService.GenerateVerificationCode("teams", externalUserId, username);
                await SendAsync(conversationId,
                    $"Your verification code: **{code}**\n\nEnter this code in OpenClaw Web UI (Settings > Channels > Link) within 5 minutes.", ct);
                break;

            case "unlink":
                await SendAsync(conversationId,
                    "To unlink your account, go to OpenClaw Web UI > Settings > Channels.", ct);
                break;

            case "help":
                await SendAsync(conversationId,
                    "**Available Commands:**\n\n" +
                    "/start - Welcome message\n" +
                    "/new - Start a new conversation\n" +
                    "/link - Link this chat to your OpenClaw account\n" +
                    "/unlink - Unlink your account\n" +
                    "/help - Show this help message", ct);
                break;
        }
    }

    private async Task ProcessAgentMessageAsync(
        string conversationId, string externalUserId, string username, string text, CancellationToken ct)
    {
        var resolvedUserId = await linkService.ResolveUserAsync("teams", externalUserId, ct);
        var conversation = await mapper.GetOrCreateConversationAsync(
            conversationId, username, resolvedUserId, repository, uow, ct);

        var history = conversation.Messages.Select(m => m.ToLlmMessage()).ToList();

        var responseBuilder = new StringBuilder();
        await foreach (var streamEvent in pipeline.ExecuteStreamAsync(text, history, userId: resolvedUserId, ct: ct))
        {
            if (streamEvent.Type == AgentStreamEventType.ContentDelta && streamEvent.Content is not null)
            {
                responseBuilder.Append(streamEvent.Content);
            }
        }

        var response = responseBuilder.ToString();
        if (string.IsNullOrWhiteSpace(response))
        {
            response = "I couldn't generate a response. Please try again.";
        }

        // Save conversation
        conversation.AddMessage(ChatRole.User, text);
        conversation.AddMessage(ChatRole.Assistant, response);
        await uow.SaveChangesAsync(ct);

        // Split long messages (Teams limit ~28KB, keep to ~4000 chars for readability)
        var chunks = SplitMessage(response, 4000);
        foreach (var chunk in chunks)
        {
            await SendAsync(conversationId, chunk, ct);
        }
    }

    private async Task SendAsync(string conversationId, string text, CancellationToken ct)
    {
        try
        {
            await adapter.SendMessageAsync(conversationId, text, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Teams message to {ConversationId}", conversationId);
        }
    }

    private static List<string> SplitMessage(string message, int maxLength)
    {
        if (message.Length <= maxLength)
            return [message];

        var chunks = new List<string>();
        var remaining = message;

        while (remaining.Length > maxLength)
        {
            var splitIndex = remaining.LastIndexOf('\n', maxLength);
            if (splitIndex <= 0) splitIndex = maxLength;

            chunks.Add(remaining[..splitIndex]);
            remaining = remaining[splitIndex..].TrimStart('\n');
        }

        if (remaining.Length > 0)
            chunks.Add(remaining);

        return chunks;
    }
}
