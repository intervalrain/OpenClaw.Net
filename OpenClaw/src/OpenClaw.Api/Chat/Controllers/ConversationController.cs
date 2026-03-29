using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Chat.Requests;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Chat.Controllers;

[ApiVersion("1.0")]
public class ConversationController(
    IConversationRepository repository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork uow) : ApiController
{
    private Guid GetUserId() => currentUserProvider.GetCurrentUser().Id;

    [HttpGet]
    public async Task<IActionResult> GetListAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        var conversations = await repository.GetAllByUserAsync(userId, ct);
        return Ok(conversations.Select(c => new
        {
            c.Id,
            c.Title,
            c.CreatedAt,
            c.UpdatedAt,
            MessageCount = c.Messages.Count
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var conversation = await repository.GetByIdAndUserAsync(id, GetUserId(), ct);
        if (conversation is null) return NotFound();
        return Ok(conversation);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest? request, CancellationToken ct)
    {
        var userId = GetUserId();
        var conversation = Conversation.Create(userId, request?.Title);
        await repository.AddAsync(conversation);
        await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = conversation.Id }, new { conversation.Id, conversation.Title });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateTitleRequest request, CancellationToken ct)
    {
        var conversation = await repository.GetByIdAndUserAsync(id, GetUserId(), ct);
        if (conversation is null) return NotFound();

        conversation.UpdateTitle(request.Title);
        await repository.UpdateAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var conversation = await repository.GetByIdAndUserAsync(id, GetUserId(), ct);
        if (conversation is null) return NotFound();
        await repository.DeleteAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
