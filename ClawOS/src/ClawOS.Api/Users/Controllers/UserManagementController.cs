using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

using ClawOS.Api.Security;
using ClawOS.Contracts.Users.Commands;
using ClawOS.Contracts.Users.Queries;
using ClawOS.Contracts.Users.Requests;

namespace ClawOS.Api.Users.Controllers;

/// <summary>
/// User management operations (SuperAdmin only).
/// </summary>
[Authorize(Roles = Role.SuperAdmin)]
[ApiVersion("1.0")]
public class UserManagementController(ISender _mediator) : ApiController
{
    /// <summary>
    /// Get all users (with optional status filter).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] string? status = null)
    {
        var query = new GetUsersQuery(status);
        var result = await _mediator.Send(query);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Get pending users awaiting approval.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingUsers()
    {
        var query = new GetUsersQuery("Pending");
        var result = await _mediator.Send(query);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Approve a pending user registration.
    /// </summary>
    [HttpPost("{userId:guid}/approve")]
    public async Task<IActionResult> ApproveUser(Guid userId)
    {
        var command = new ApproveUserCommand(userId);
        var result = await _mediator.Send(command);

        return result.Match(_ => Ok(new { message = "User approved successfully" }), Problem);
    }

    /// <summary>
    /// Reject a pending user registration.
    /// </summary>
    [HttpPost("{userId:guid}/reject")]
    public async Task<IActionResult> RejectUser(Guid userId)
    {
        var command = new RejectUserCommand(userId);
        var result = await _mediator.Send(command);

        return result.Match(_ => Ok(new { message = "User rejected and removed" }), Problem);
    }

    /// <summary>
    /// Update user status (activate, deactivate, lock).
    /// </summary>
    [HttpPut("{userId:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UpdateUserStatusRequest request)
    {
        var command = new UpdateUserStatusCommand(userId, request.Status);
        var result = await _mediator.Send(command);

        return result.Match(_ => Ok(new { message = "User status updated" }), Problem);
    }

    /// <summary>
    /// Update user roles.
    /// </summary>
    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> UpdateUserRoles(Guid userId, [FromBody] UpdateUserRolesRequest request)
    {
        var command = new UpdateUserRolesCommand(userId, request.Roles, request.Permissions);
        var result = await _mediator.Send(command);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Ban a user with a reason. Cannot ban Admin or SuperAdmin.
    /// </summary>
    [HttpPost("{userId:guid}/ban")]
    public async Task<IActionResult> BanUser(Guid userId, [FromBody] BanUserRequest request)
    {
        var command = new BanUserCommand(userId, request.Reason);
        var result = await _mediator.Send(command);

        return result.Match(_ =>
        {
            BanCheckMiddleware.InvalidateUser(userId);
            return Ok(new { message = "User banned" });
        }, Problem);
    }

    /// <summary>
    /// Unban a user, restoring Active status.
    /// </summary>
    [HttpPost("{userId:guid}/unban")]
    public async Task<IActionResult> UnbanUser(Guid userId)
    {
        var command = new UnbanUserCommand(userId);
        var result = await _mediator.Send(command);

        return result.Match(_ =>
        {
            BanCheckMiddleware.InvalidateUser(userId);
            return Ok(new { message = "User unbanned" });
        }, Problem);
    }

    /// <summary>
    /// Delete a user.
    /// </summary>
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var command = new DeleteUserCommand(userId);
        var result = await _mediator.Send(command);

        return result.Match(_ => NoContent(), Problem);
    }
}

public record UpdateUserStatusRequest(string Status);
public record BanUserRequest(string Reason);
