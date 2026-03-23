using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Weda.Core.Presentation;

using OpenClaw.Application.Auth.Commands;
using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Auth.Requests;
using OpenClaw.Contracts.Auth.Responses;

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
    
    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await _mediator.Send(command);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Register a new user account (requires admin approval).
    /// </summary>
    /// <param name="request">The registration details.</param>
    /// <returns>Registration confirmation (user will be in pending status).</returns>
    /// <response code="200">Registration submitted, pending admin approval.</response>
    /// <response code="400">Invalid request data or email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var command = new RegisterCommand(request.Email, request.Password, request.Name);
        var result = await _mediator.Send(command);

        return result.Match(Ok, Problem);
    }
}