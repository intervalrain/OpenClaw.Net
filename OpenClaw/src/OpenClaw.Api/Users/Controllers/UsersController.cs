using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Contracts.Users.Queries;
using OpenClaw.Contracts.Users.Requests;

namespace OpenClaw.Api.Users.Controllers;

/// <summary>
/// Manages user operations including CRUD and role management.
/// </summary>
[ApiVersion("1.0")]
[Authorize]
public class UsersController(ISender mediator) : ApiController
{
    /// <summary>
    /// Retrieves the current authenticated user's information.
    /// </summary>
    /// <returns>The current user's details.</returns>
    /// <response code="200">Returns the current user.</response>
    /// <response code="401">User not authenticated.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var query = new GetCurrentUserQuery();
        var result = await mediator.Send(query);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Retrieves all users (Admin or above).
    /// </summary>
    /// <returns>A list of all users in the system.</returns>
    /// <response code="200">Returns the list of users.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User does not have Admin or SuperAdmin role.</response>
    [HttpGet]
    [Authorize(Policy = Policy.AdminOrAbove)]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var query = new ListUsersQuery();
        var result = await mediator.Send(query);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Retrieves a specific user by ID (self or Admin).
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <returns>The user details.</returns>
    /// <response code="200">Returns the user.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User can only access their own data unless Admin or SuperAdmin.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetUserQuery(id);
        var result = await mediator.Send(query);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Creates a new user (Admin or above).
    /// </summary>
    /// <param name="request">The user creation details.</param>
    /// <returns>The created user.</returns>
    /// <response code="201">User created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User does not have Admin or SuperAdmin role.</response>
    /// <response code="409">A user with this email already exists.</response>
    [HttpPost]
    [Authorize(Policy = Policy.AdminOrAbove)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var command = new CreateUserCommand(
            request.Email,
            request.Password,
            request.Name);

        var result = await mediator.Send(command);

        return result.Match(
            user => CreatedAtAction(nameof(GetById), new { id = user.Id }, user),
            Problem);
    }

    /// <summary>
    /// Updates an existing user (self or Admin).
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="request">The updated user details.</param>
    /// <returns>The updated user.</returns>
    /// <response code="200">User updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User can only update their own data unless Admin or SuperAdmin.</response>
    /// <response code="404">User not found.</response>
    /// <response code="409">A user with this email already exists.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand(
            id,
            request.Name,
            request.Password);

        var result = await mediator.Send(command);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Updates a user's roles and permissions (SuperAdmin only).
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="request">The new roles and permissions.</param>
    /// <returns>The updated user.</returns>
    /// <response code="200">Roles updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User does not have SuperAdmin role.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("{id:guid}/roles")]
    [Authorize(Policy = Policy.SuperAdminOnly)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoles(Guid id, [FromBody] UpdateUserRolesRequest request)
    {
        var command = new UpdateUserRolesCommand(id, request.Roles, request.Permissions);
        var result = await mediator.Send(command);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Deletes a user (Admin or above).
    /// </summary>
    /// <param name="id">The unique identifier of the user to delete.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">User deleted successfully.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User does not have Admin or SuperAdmin role, or trying to delete SuperAdmin.</response>
    /// <response code="404">User not found.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policy.AdminOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteUserCommand(id);
        var result = await mediator.Send(command);

        return result.Match(_ => NoContent(), Problem);
    }
}
