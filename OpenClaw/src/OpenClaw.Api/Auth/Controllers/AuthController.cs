using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Weda.Core.Presentation;

using OpenClaw.Api.Security;
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
public class AuthController(ISender _mediator, LoginRateLimiter rateLimiter, RegistrationRateLimiter registrationRateLimiter) : ApiController
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

    [HttpPost("register")]
    [ProducesResponseType(typeof(InitiateRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email.ToLowerInvariant();
        if (registrationRateLimiter.IsRegistrationLimited(email))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Too many registration attempts. Please try again later." });

        registrationRateLimiter.RecordRegistration(email);

        var command = new InitiateRegistrationCommand(request.Email, request.Password, request.Name);
        var result = await _mediator.Send(command);
        return result.Match(Ok, Problem);
    }

    [HttpPost("register/verify")]
    [ProducesResponseType(typeof(VerifyRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyRegistration([FromBody] VerifyRegistrationRequest request)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var command = new VerifyRegistrationCommand(request.Email, request.Code, baseUrl);
        var result = await _mediator.Send(command);
        return result.Match(Ok, Problem);
    }

    [HttpPost("register/resend")]
    [ProducesResponseType(typeof(ResendVerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        var email = request.Email.ToLowerInvariant();
        if (registrationRateLimiter.IsResendLimited(email))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Too many resend attempts. Please try again later." });

        registrationRateLimiter.RecordResend(email);

        var command = new ResendVerificationCommand(request.Email);
        var result = await _mediator.Send(command);
        return result.Match(Ok, Problem);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var command = new ForgotPasswordCommand(request.Email, baseUrl);
        var result = await _mediator.Send(command);
        return result.Match(Ok, Problem);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword);
        var result = await _mediator.Send(command);
        return result.Match(Ok, Problem);
    }
}
