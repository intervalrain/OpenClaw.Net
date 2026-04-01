using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Weda.Core.Presentation.Filters;

/// <summary>
/// Global exception handler using .NET 8+ IExceptionHandler pattern.
/// Implements RFC 9457 Problem Details for HTTP APIs.
/// For developers: Any 500 indicates an unhandled exception that should be converted to ErrorOr pattern.
/// </summary>
public class GlobalExceptionHandler(
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception, 
            "Unhandled exception occurred. RequestPath: {RequestPath}, TraceId: {TraceId}",
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred",
            Type = "https://httpstatuses.io/500",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier
            }
        };

        // Only include exception details in development
        if (environment.IsDevelopment())
        {
            problemDetails.Detail = exception.Message;
            problemDetails.Extensions["exception"] = new
            {
                type = exception.GetType().Name,
                message = exception.Message,
                StackTrace = exception.StackTrace
            };
        }
        else
        {
            problemDetails.Detail = "Please contact support with the traceId for assistance.";
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json"; // RFC 9457 media type

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // Exception was handled
    }
}