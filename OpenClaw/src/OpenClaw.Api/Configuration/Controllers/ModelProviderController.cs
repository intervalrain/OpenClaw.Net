using System.Text.Json;

using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Configuration.Dtos;
using OpenClaw.Contracts.Configuration.Requests;
using OpenClaw.Contracts.Configuration.Responses;
using OpenClaw.Contracts.Security;
using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[ApiVersion("1.0")]
public class ModelProviderController(
    IModelProviderRepository repository,
    IEncryptionService encryption,
    IUnitOfWork uow,
    IHttpClientFactory httpClientFactory) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> ListProviders(CancellationToken ct)
    {
        var providers = await repository.GetAllAsync(ct);
        var result = providers.Select(p => new ModelProviderDto(
            p.Id,
            p.Type,
            p.Name,
            p.Url,
            p.ModelName,
            Masking(p.EncryptedApiKey),
            p.IsActive,
            p.CreatedAt));

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProvider(Guid id, CancellationToken ct)
    {
        var provider = await repository.GetByIdAsync(id, ct);
        return provider is null
            ? NotFound()
            : Ok(new ModelProviderDto(
            provider.Id,
            provider.Type,
            provider.Name,
            provider.Url,
            provider.ModelName,
            Masking(provider.EncryptedApiKey),
            provider.IsActive,
            provider.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> CreateProvider(CreateModelProviderRequest request, CancellationToken ct)
    {
        var encryptedKey = string.IsNullOrEmpty(request.ApiKey) ? null : encryption.Encrypt(request.ApiKey);
        var provider = ModelProvider.Create(
            request.Type,
            request.Name,
            request.Url,
            request.ModelName,
            encryptedKey,
            request.IsActive);

        await repository.AddAsync(provider, ct);
        await uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetProvider), new { id = provider.Id }, new { id = provider.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProvider(Guid id, [FromBody] UpdateModelProviderRequest request, CancellationToken ct)
    {
        var provider = await repository.GetByIdAsync(id, ct);
        if (provider is null) return NotFound();

        var encryptedKey = string.IsNullOrEmpty(request.ApiKey)
            ? null
            : encryption.Encrypt(request.ApiKey);

        provider.Update(request.Name, request.Url, request.ModelName, encryptedKey);
        await uow.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var provider = await repository.GetByIdAsync(id, ct);
        if (provider is null) return NotFound();

        await repository.DeleteAsync(provider, ct);
        await uow.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var provider = await repository.GetByIdAsync(id, ct);
        if (provider is null) return NotFound();

        if (provider.IsActive) return Ok();
        var deactivatedProvider = await repository.GetActiveAsync(ct);
        if (deactivatedProvider != null)
        {
            deactivatedProvider.Deactivate();
        }
        provider.Activate();
        await uow.SaveChangesAsync(ct);

        return Ok();
    }

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
                {
                    return BadRequest(new { success = false, message = "Cannot connect to Ollama server" });
                }

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
                    return BadRequest(new {
                        success = false,
                        message = $"Model \"{request.ModelName}\" not found. Available: {availableModels}"
                    });
                }

                return Ok(new { success = true, message = "Validation successful" });
            }
            else if (request.Type == "openai")
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                requestMessage.Headers.Add("Authorization", $"Bearer {request.ApiKey}");

                var response = await client.SendAsync(requestMessage, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new { success = false, message = "Invalid OpenAI API Key" });
                }

                return Ok(new { success = true, message = "Validation successful" });
            }
            else if (request.Type == "anthropic")
            {
                if (string.IsNullOrEmpty(request.ApiKey) || !request.ApiKey.StartsWith("sk-ant-"))
                {
                    return BadRequest(new {
                        success = false,
                        message = "Invalid Anthropic API Key format (should start with sk-ant-)"
                    });
                }

                return Ok(new { success = true, message = "Validation successful" });
            }
            else if (request.Type == "custom")
            {
                // For custom providers, just check URL is reachable
                var url = request.Url.TrimEnd('/');
                try
                {
                    var response = await client.GetAsync(url, ct);
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

    private static string? Masking(string? encryptedApiKey)
    {
        return string.IsNullOrEmpty(encryptedApiKey) ? null : "********************************";
    }
}