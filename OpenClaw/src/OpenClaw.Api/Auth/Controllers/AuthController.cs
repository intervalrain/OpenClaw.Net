using Asp.Versioning;


using Mediator;


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Weda.Core.Presentation;
using OpenClaw.Contracts.Auth;
using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Auth.Requests;

namespace OpenClaw.Api.Auth.Controllers;

/// <summary>
/// Authentication and authorization operations.
/// </summary>
[AllowAnonymous]
[ApiVersion("1.0")]
public class AuthController(ISender _mediator) : ApiController
{
    /// <summary>
    /// Login with email and password.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <returns>The authentication response with JWT token.</returns>
    /// <response code="200">Login successful, returns JWT token.</response>
    /// <response code="401">Invalid credentials or account not active.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command);

        return result.Match(Ok, Problem);
    }
}