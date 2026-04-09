using Asp.Versioning;

using Microsoft.Extensions.Logging;

using OpenClaw.Channels.Teams.Services;
using OpenClaw.Contracts.Channels;

using Weda.Core.Infrastructure.Messaging.Nats;
using Weda.Core.Infrastructure.Messaging.Nats.Attributes;

namespace OpenClaw.Channels.Teams.EventControllers;

[ApiVersion("1")]
public class TeamsEventController(ITeamsMessageService messageService) : EventController
{
    [Subject("eco1j.weda.{messageId}.openclaw.teams.doc")]
    public async Task OnMessageReceived(ChannelMessageReceivedEvent @event)
    {
        Logger.LogInformation(
            "Dispatching Teams message {MessageId} from {ConversationId}",
            @event.ExternalMessageId, @event.ExternalChatId);

        await messageService.HandleMessageAsync(@event);
    }
}
