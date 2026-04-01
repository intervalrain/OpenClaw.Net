using System.Text.Json;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ClawOS.Contracts.Configuration.Dtos;
using ClawOS.Contracts.Configuration.Requests;
using ClawOS.Contracts.Configuration.Responses;
using ClawOS.Contracts.Security;
using ClawOS.Domain.Configuration.Entities;
using ClawOS.Domain.Configuration.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace ClawOS.Api.Configuration.Controllers;

/// <summary>
/// User-facing model provider management.
/// Users can browse available global providers and manage their own provider list.
/// </summary>
[ApiVersion("1.0")]
[Authorize]
public class UserModelProviderController(
    IUserModelProviderRepository userRepository,
    IModelProviderRepository globalRepository,
    IEncryptionService encryption,
    ICurrentUserProvider currentUserProvider,
    IHttpClientFactory httpClientFactory,
    IUnitOfWork uow) : ApiController
{
    private Guid GetUserId() => currentUserProvider.GetCurrentUser().Id;

    /// <summary>
    /// List all available global providers that the user can add.
    /// </summary>
    [HttpGet("available")]
    public async Task<IActionResult> ListAvailable(CancellationToken ct)
    {
        var providers = await globalRepository.GetAllActiveAsync(ct);
        var result = providers.Select(p => new AvailableModelProviderDto(
            p.Id,
            p.Type,
            p.Name,
            p.ModelName,
            p.Description));

        return Ok(result);
    }

    /// <summary>
    /// List the current user's own model providers.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var userId = GetUserId();
        var providers = await userRepository.GetAllByUserAsync(userId, ct);

        var globalIds = providers
            .Where(p => p.GlobalModelProviderId.HasValue)
            .Select(p => p.GlobalModelProviderId!.Value)
            .ToHashSet();

        // Batch-load global provider names
        var globalProviders = new Dictionary<Guid, string>();
        if (globalIds.Count > 0)
        {
            var globals = await globalRepository.GetAllAsync(ct);
            foreach (var g in globals.Where(g => globalIds.Contains(g.Id)))
                globalProviders[g.Id] = g.Name;
        }

        var result = providers.Select(p => new UserModelProviderDto(
            p.Id,
            p.Type,
            p.Name,
            p.Url,
            p.ModelName,
            Masking(p.EncryptedApiKey),
            p.GlobalModelProviderId,
            p.GlobalModelProviderId.HasValue && globalProviders.TryGetValue(p.GlobalModelProviderId.Value, out var gName) ? gName : null,
            p.IsDefault,
            p.CreatedAt));

        return Ok(result);
    }

    /// <summary>
    /// Add a model provider to the user's list.
    /// Either reference a global provider or create a custom one with own API key.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserModelProviderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        // Check name uniqueness per user
        var existing = await userRepository.GetByNameAsync(userId, request.Name, ct);
        if (existing is not null)
            return Conflict(new { message = $"You already have a provider named '{request.Name}'" });

        UserModelProvider provider;

        if (request.GlobalModelProviderId.HasValue)
        {
            // Reference a global provider
            var global = await globalRepository.GetByIdAsync(request.GlobalModelProviderId.Value, ct);
            if (global is null || !global.IsActive)
                return BadRequest(new { message = "Global provider not found or not active" });

            provider = UserModelProvider.CreateFromGlobal(
                userId,
                global.Id,
                global.Type,
                request.Name,
                global.Url,
                global.ModelName,
                request.IsDefault);
        }
        else
        {
            // Custom provider with user's own credentials
            if (string.IsNullOrEmpty(request.Type) || string.IsNullOrEmpty(request.Url) || string.IsNullOrEmpty(request.ModelName))
                return BadRequest(new { message = "Type, Url, and ModelName are required for custom providers" });

            var encryptedKey = string.IsNullOrEmpty(request.ApiKey) ? null : encryption.Encrypt(request.ApiKey);

            provider = UserModelProvider.CreateCustom(
                userId,
                request.Type,
                request.Name,
                request.Url,
                request.ModelName,
                encryptedKey,
                request.IsDefault);
        }

        // If setting as default, clear previous default
        if (request.IsDefault)
            await ClearUserDefault(userId, ct);

        await userRepository.AddAsync(provider, ct);
        await uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListMine), new { id = provider.Id });
    }

    /// <summary>
    /// Update a user's own model provider.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserModelProviderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var provider = await userRepository.GetByIdAsync(id, ct);
        if (provider is null || provider.UserId != userId)
            return NotFound();

        if (provider.GlobalModelProviderId.HasValue)
            return BadRequest(new { message = "Cannot modify a global-referenced provider. Remove it and add a custom one instead." });

        var encryptedKey = string.IsNullOrEmpty(request.ApiKey)
            ? null
            : encryption.Encrypt(request.ApiKey);

        provider.Update(request.Name, request.Url, request.ModelName, encryptedKey);
        await uow.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Delete a user's own model provider.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var provider = await userRepository.GetByIdAsync(id, ct);
        if (provider is null || provider.UserId != userId)
            return NotFound();

        await userRepository.DeleteAsync(provider, ct);
        await uow.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Set a provider as the user's default.
    /// </summary>
    [HttpPost("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var provider = await userRepository.GetByIdAsync(id, ct);
        if (provider is null || provider.UserId != userId)
            return NotFound();

        await ClearUserDefault(userId, ct);
        provider.SetDefault(true);
        await uow.SaveChangesAsync(ct);

        return Ok();
    }

    /// <summary>
    /// Validate a model provider connection before saving.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateModelProviderRequest request, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("SkipSslValidation");

            if (request.Type == "ollama")
            {
                var url = request.Url.TrimEnd('/');
                var response = await client.GetAsync($"{url}/api/tags", ct);

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new { success = false, message = "Cannot connect to Ollama server" });

                var content = await response.Content.ReadAsStringAsync(ct);
                var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var models = tagsResponse?.Models ?? [];
                var modelExists = models.Any(m =>
                    m.Name == request.ModelName ||
                    m.Name.StartsWith(request.ModelName + ":"));

                if (!modelExists)
                {
                    var availableModels = string.Join(", ", models.Take(5).Select(m => m.Name));
                    return BadRequest(new { success = false, message = $"Model \"{request.ModelName}\" not found. Available: {availableModels}" });
                }

                return Ok(new { success = true, message = "Validation successful" });
            }
            else if (request.Type == "openai")
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                requestMessage.Headers.Add("Authorization", $"Bearer {request.ApiKey}");
                var response = await client.SendAsync(requestMessage, ct);

                return !response.IsSuccessStatusCode
                    ? BadRequest(new { success = false, message = "Invalid OpenAI API Key" })
                    : Ok(new { success = true, message = "Validation successful" });
            }
            else if (request.Type == "anthropic")
            {
                if (string.IsNullOrEmpty(request.ApiKey) || !request.ApiKey.StartsWith("sk-ant-"))
                    return BadRequest(new { success = false, message = "Invalid Anthropic API Key format (should start with sk-ant-)" });

                return Ok(new { success = true, message = "Validation successful" });
            }
            else if (request.Type == "custom")
            {
                var url = request.Url.TrimEnd('/');
                try
                {
                    await client.GetAsync(url, ct);
                    return Ok(new { success = true, message = "URL is reachable" });
                }
                catch
                {
                    return BadRequest(new { success = false, message = "Cannot reach the specified URL" });
                }
            }

            return BadRequest(new { success = false, message = "Unknown provider type" });
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

    private async Task ClearUserDefault(Guid userId, CancellationToken ct)
    {
        var currentDefault = await userRepository.GetDefaultAsync(userId, ct);
        currentDefault?.SetDefault(false);
    }

    private static string? Masking(string? encryptedApiKey)
    {
        return string.IsNullOrEmpty(encryptedApiKey) ? null : "********************************";
    }
}
