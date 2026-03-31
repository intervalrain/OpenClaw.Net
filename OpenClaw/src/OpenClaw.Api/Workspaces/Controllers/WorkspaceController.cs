using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Domain.Workspaces.Entities;
using OpenClaw.Domain.Workspaces.Repositories;
using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Workspaces.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class WorkspaceController(
    IWorkspaceRepository repository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork uow) : ApiController
{
    /// <summary>
    /// Get all workspaces the current user belongs to.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyWorkspaces(CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var workspaces = await repository.GetByUserAsync(user.Id, ct);

        return Ok(workspaces.Select(w => new
        {
            w.Id,
            w.Name,
            w.Description,
            w.IsPersonal,
            w.OwnerUserId,
            MyRole = w.GetMemberRole(user.Id)?.ToString(),
            MemberCount = w.Members.Count,
            w.CreatedAt
        }));
    }

    /// <summary>
    /// Get workspace details with members.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var workspace = await repository.GetByIdWithMembersAsync(id, ct);

        if (workspace is null)
            return NotFound();

        if (!workspace.IsMember(user.Id) && !user.Roles.Contains(Role.SuperAdmin))
            return Forbid();

        return Ok(new
        {
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.IsPersonal,
            workspace.OwnerUserId,
            Members = workspace.Members.Select(m => new
            {
                m.Id,
                m.UserId,
                Role = m.Role.ToString(),
                m.JoinedAt
            }),
            workspace.CreatedAt
        });
    }

    /// <summary>
    /// Create a shared workspace.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var workspace = Workspace.CreateShared(user.Id, request.Name, request.Description);

        await repository.AddAsync(workspace, ct);
        await uow.SaveChangesAsync(ct);

        return Ok(new { workspace.Id, workspace.Name });
    }

    /// <summary>
    /// Update workspace name/description. Owner or SuperAdmin only.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceRequest request, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var workspace = await repository.GetByIdWithMembersAsync(id, ct);

        if (workspace is null) return NotFound();
        if (workspace.IsPersonal) return BadRequest("Cannot rename personal workspace");
        if (workspace.OwnerUserId != user.Id && !user.Roles.Contains(Role.SuperAdmin))
            return Forbid();

        workspace.Update(request.Name, request.Description);
        await uow.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Add a member to workspace. Owner or SuperAdmin only.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var workspace = await repository.GetByIdWithMembersAsync(id, ct);

        if (workspace is null) return NotFound();
        if (workspace.OwnerUserId != user.Id && !user.Roles.Contains(Role.SuperAdmin))
            return Forbid();

        var role = Enum.TryParse<WorkspaceRole>(request.Role, true, out var r) ? r : WorkspaceRole.Member;

        try
        {
            workspace.AddMember(request.UserId, role);
            await uow.SaveChangesAsync(ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Remove a member from workspace. Owner or SuperAdmin only.
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var workspace = await repository.GetByIdWithMembersAsync(id, ct);

        if (workspace is null) return NotFound();
        if (workspace.OwnerUserId != user.Id && !user.Roles.Contains(Role.SuperAdmin))
            return Forbid();

        try
        {
            workspace.RemoveMember(userId);
            await uow.SaveChangesAsync(ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Delete a workspace. Owner or SuperAdmin only. Cannot delete personal workspace.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var workspace = await repository.GetByIdWithMembersAsync(id, ct);

        if (workspace is null) return NotFound();
        if (workspace.IsPersonal) return BadRequest("Cannot delete personal workspace");
        if (workspace.OwnerUserId != user.Id && !user.Roles.Contains(Role.SuperAdmin))
            return Forbid();

        await repository.DeleteAsync(workspace, ct);
        await uow.SaveChangesAsync(ct);
        return Ok();
    }
}

public record CreateWorkspaceRequest(string Name, string? Description = null);
public record UpdateWorkspaceRequest(string Name, string? Description = null);
public record AddMemberRequest(Guid UserId, string? Role = null);
