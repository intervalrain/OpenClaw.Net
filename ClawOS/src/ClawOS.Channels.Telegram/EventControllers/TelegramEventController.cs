using Asp.Versioning;

using Microsoft.Extensions.Logging;

using ClawOS.Channels.Telegram.Services;
using ClawOS.Contracts.Channels;

using Weda.Core.Infrastructure.Messaging.Nats;
using Weda.Core.Infrastructure.Messaging.Nats.Attributes;

namespace ClawOS.Channels.Telegram.EventControllers;

[ApiVersion("1")]
public class TelegramEventController(ITelegramMessageService messageService) : EventController
{
    [Subject("eco1j.weda.{messageId}.clawos.telegram.doc")]
    public async Task OnMessageReceived(ChannelMessageReceivedEvent @event)
    {
        Logger.LogInformation(
            "Dispatching Telegram message {MessageId} from {ChatId}",
            @event.ExternalMessageId, @event.ExternalChatId);

        await messageService.HandleMessageAsync(@event);
    }
}