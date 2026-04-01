using FluentValidation;

using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;

using ClawOS.Channels.Telegram.Errors;

using ClawOS.Channels.Telegram.Models;

using Weda.Core.Application.Validation;

namespace ClawOS.Channels.Telegram.Services.Commands;

public class HandleTelegramMessageCommandValidator : AbstractValidator<HandleTelegramMessageCommand>
{
    public HandleTelegramMessageCommandValidator(IOptions<TelegramBotOptions> options)
    {
        var allowedUserIds = options.Value.AllowedUserIds;

        RuleFor(x => x.Event.ExternalChatId)
            .NotEmpty()
            .Must(IsValidLong)
            .WithError(x => TelegramErrors.InvalidChatId(x.Event.ExternalChatId));

        RuleFor(x => x.Event.ExternalUserId)
            .NotEmpty()
            .Must(IsValidLong)
            .WithError(x => TelegramErrors.InvalidUserId(x.Event.ExternalUserId));

        RuleFor(x => x.Event.Content)
            .NotEmpty()
            .WithError(TelegramErrors.EmptyMessage);

        When(_ => allowedUserIds.Length > 0, () =>
        {
            RuleFor(x => x.Event.ExternalUserId)
                .Must(userId => long.TryParse(userId, out var id) && allowedUserIds.Contains(id))
                .WithError(x => TelegramErrors.UnauthorizedUser(
                    long.TryParse(x.Event.ExternalUserId, out var id) ? id : 0));
        });
    }   

    private static bool IsValidLong(string? value) => long.TryParse(value, out _); 
}