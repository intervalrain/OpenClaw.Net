using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Configuration.Dtos;
using OpenClaw.Contracts.Configuration.Requests;
using OpenClaw.Contracts.Security;
using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[ApiVersion("1.0")]
public class ChannelSettingsController(
    IChannelSettingsRepository repository,
    IEncryptionService encryption,
    IUnitOfWork uow,
    IHttpClientFactory httpClientFactory) : ApiController
{
    private const string TelegramChannelType = "telegram";

    [HttpGet("telegram")]
    public async Task<IActionResult> GetTelegramSettings(CancellationToken ct)
    {
        var settings = await repository.GetByChannelTypeAsync(TelegramChannelType, ct);
        if (settings is null)
        {
            return Ok(new ChannelSettingsDto(
                Guid.Empty,
                TelegramChannelType,
                false,
                null,
                null,
                null,
                [],
                DateTime.UtcNow,
                null));
        }

        return Ok(new ChannelSettingsDto(
            settings.Id,
            settings.ChannelType,
            settings.Enabled,
            MaskBotToken(settings.EncryptedBotToken),
            settings.WebhookUrl,
            settings.SecretToken,
            settings.GetAllowedUserIdsList(),
            settings.CreatedAt,
            settings.UpdatedAt));
    }

    [HttpPut("telegram")]
    public async Task<IActionResult> UpdateTelegramSettings(
        [FromBody] UpdateChannelSettingsRequest request,
        CancellationToken ct)
    {
        var settings = await repository.GetByChannelTypeAsync(TelegramChannelType, ct);

        var encryptedToken = string.IsNullOrEmpty(request.BotToken)
            ? null
            : encryption.Encrypt(request.BotToken);

        var allowedUserIds = string.Join(",", request.AllowedUserIds);

        if (settings is null)
        {
            settings = ChannelSettings.Create(
                TelegramChannelType,
                request.Enabled,
                encryptedToken,
                request.WebhookUrl,
                request.SecretToken,
                allowedUserIds);

            await repository.AddAsync(settings, ct);
        }
        else
        {
            settings.Update(
                request.Enabled,
                encryptedToken,
                request.WebhookUrl,
                request.SecretToken,
                allowedUserIds);
        }

        await uow.SaveChangesAsync(ct);

        return Ok(new ChannelSettingsDto(
            settings.Id,
            settings.ChannelType,
            settings.Enabled,
            MaskBotToken(settings.EncryptedBotToken),
            settings.WebhookUrl,
            settings.SecretToken,
            settings.GetAllowedUserIdsList(),
            settings.CreatedAt,
            settings.UpdatedAt));
    }

    [HttpPost("telegram/validate")]
    public async Task<IActionResult> ValidateTelegramBot(
        [FromBody] ValidateTelegramBotRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BotToken))
        {
            return BadRequest(new { success = false, message = "Bot token is required" });
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{request.BotToken}/getMe";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { success = false, message = "Invalid bot token" });
            }

            var content = await response.Content.ReadFromJsonAsync<TelegramApiResponse>(ct);
            if (content?.Ok != true || content.Result is null)
            {
                return BadRequest(new { success = false, message = "Invalid bot token" });
            }

            return Ok(new
            {
                success = true,
                message = "Validation successful",
                botInfo = new
                {
                    id = content.Result.Id,
                    firstName = content.Result.FirstName,
                    username = content.Result.Username,
                    canJoinGroups = content.Result.CanJoinGroups,
                    canReadAllGroupMessages = content.Result.CanReadAllGroupMessages
                }
            });
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { success = false, message = $"Connection failed: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"Validation failed: {ex.Message}" });
        }
    }

    private static string? MaskBotToken(string? encryptedToken)
    {
        return string.IsNullOrEmpty(encryptedToken)
            ? null
            : "********************************";
    }

    private record TelegramApiResponse(bool Ok, TelegramBotInfo? Result);
    private record TelegramBotInfo(
        long Id,
        bool IsBot,
        string FirstName,
        string? Username,
        bool? CanJoinGroups,
        bool? CanReadAllGroupMessages);
}
