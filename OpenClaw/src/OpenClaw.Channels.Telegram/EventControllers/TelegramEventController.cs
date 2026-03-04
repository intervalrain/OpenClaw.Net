using Asp.Versioning;

using Microsoft.Extensions.Logging;

using OpenClaw.Channels.Telegram.Services;
using OpenClaw.Contracts.Channels;

using Weda.Core.Infrastructure.Messaging.Nats;
using Weda.Core.Infrastructure.Messaging.Nats.Attributes;

namespace OpenClaw.Channels.Telegram.EventControllers;

[ApiVersion("1")]
public class TelegramEventController(ITelegramMessageService messageService) : EventController
{
    [Subject("eco1j.weda.{messageId}.openclaw.telegram.doc")]
    public async Task OnMessageReceived(ChannelMessageReceivedEvent @event)
    {
        Logger.LogInformation(
            "Dispatching Telegram message {MessageId} from {ChatId}",
            @event.ExternalMessageId, @event.ExternalChatId);

        await messageService.HandleMessageAsync(@event);
    }
}