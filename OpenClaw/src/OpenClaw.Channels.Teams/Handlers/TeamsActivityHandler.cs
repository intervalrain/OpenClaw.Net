using System.Collections.Concurrent;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Channels;

using Weda.Core.Application.Interfaces.Messaging;

namespace OpenClaw.Channels.Teams.Handlers;

/// <summary>
/// Handles incoming Bot Framework Activities from Microsoft Teams.
/// Converts to ChannelMessageReceivedEvent and publishes to JetStream.
/// Stores ConversationReference for proactive messaging.
/// </summary>
public class TeamsActivityHandler(
    IJetStreamClientFactory jetStreamFactory,
    ILogger<TeamsActivityHandler> logger) : ActivityHandler
{
    /// <summary>
    /// In-memory store of conversation references for proactive messaging.
    /// Key: conversationId (from Activity.Conversation.Id)
    /// </summary>
    public ConcurrentDictionary<string, ConversationReference> ConversationReferences { get; } = new();

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken ct)
    {
        var activity = turnContext.Activity;
        var text = activity.Text?.Trim();

        if (string.IsNullOrEmpty(text)) return;

        // Store conversation reference for later proactive messaging
        var reference = activity.GetConversationReference();
        ConversationReferences.AddOrUpdate(
            activity.Conversation.Id,
            reference,
            (_, _) => reference);

        // Build platform-agnostic event
        var @event = new ChannelMessageReceivedEvent(
            ChannelName: "teams",
            ExternalChatId: activity.Conversation.Id,
            ExternalUserId: activity.From.Id,
            ExternalUsername: activity.From.Name,
            Content: text,
            ExternalMessageId: activity.Id,
            Type: ChannelMessageType.Text,
            ReceivedAt: DateTimeOffset.UtcNow);

        // Publish to JetStream for async processing
        var bus = jetStreamFactory.Create();
        var subject = $"eco1j.weda.{@event.ExternalMessageId}.openclaw.teams.doc";
        await bus.JsPublishAsync(subject, @event);

        logger.LogDebug("Published Teams message {MessageId} from {User} to JetStream",
            activity.Id, activity.From.Name);
    }

    protected override async Task OnMembersAddedAsync(
        IList<ChannelAccount> membersAdded,
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken ct)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Welcome to OpenClaw! Send me a message and I'll help you. Use /help for commands."),
                    ct);
            }
        }
    }
}
