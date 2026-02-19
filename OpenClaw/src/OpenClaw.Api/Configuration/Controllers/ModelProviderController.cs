using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Configuration.Dtos;
using OpenClaw.Contracts.Configuration.Requests;
using OpenClaw.Contracts.Security;
using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
public class ModelProviderController(
    IModelProviderRepository repository,
    IEncryptionService encryption,
    IUnitOfWork uow) : ApiController
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

    private static string? Masking(string? encryptedApiKey)
    {
        return string.IsNullOrEmpty(encryptedApiKey) ? null : "********************************";
    }
}