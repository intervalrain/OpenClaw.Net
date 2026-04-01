using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Weda.Core.Presentation;

using ClawOS.Api.Security;
using ClawOS.Application.Auth.Commands;
using ClawOS.Contracts.Auth.Commands;
using ClawOS.Contracts.Auth.Requests;
using ClawOS.Contracts.Auth.Responses;

namespace ClawOS.Api.Auth.Controllers;

/// <summary>
/// Authentication and authorization operations.
/// </summary>
[AllowAnonymous]
[ApiVersion("1.0")]
public class AuthController(ISender _mediator, LoginRateLimiter rateLimiter) : ApiController
{
    /// <summary>
    /// Login with email and password.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <returns>The authentication response with JWT token.</returns>
    /// <response code="200">Login successful, returns JWT token.</response>
    /// <response code="401">Invalid credentials or account not active.</response>
    /// <response code="429">Too many failed attempts, account temporarily locked.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var rateLimitKey = request.Email.ToLowerInvariant();

        // Check account lockout
        if (rateLimiter.IsLockedOut(rateLimitKey))
        {
            var remaining = rateLimiter.GetRemainingLockout(rateLimitKey);
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = $"Too many failed login attempts. Try again in {remaining?.Minutes ?? 15} minutes." });
        }

        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command);

        if (result.IsError)
        {
            rateLimiter.RecordFailedAttempt(rateLimitKey);
            return Problem(result.Errors);
        }

        rateLimiter.RecordSuccessfulLogin(rateLimitKey);
        return Ok(result.Value);
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
