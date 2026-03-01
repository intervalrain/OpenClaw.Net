using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Chat.Requests;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Chat.Controllers;

[ApiVersion("1.0")]
public class ConversationController(IConversationRepository repository, IUnitOfWork uow) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetListAsync(CancellationToken ct)
    {
        var conversations = await repository.GetAllAsync(ct);
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
        var conversation = await repository.GetByIdAsync(id, ct);
        if (conversation is null) return NotFound();
        return Ok(conversation);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest? request, CancellationToken ct)
    {
        var conversation = Conversation.Create(request?.Title);
        await repository.AddAsync(conversation);
        await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = conversation.Id }, new { conversation.Id, conversation.Title });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateTitleRequest request, CancellationToken ct)
    {
        var conversation = await repository.GetByIdAsync(id, ct);
        if (conversation is null) return NotFound();

        conversation.UpdateTitle(request.Title);
        await repository.UpdateAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var conversation = await repository.GetByIdAsync(id, ct);
        if (conversation is null) return NotFound();
        await repository.DeleteAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }
}