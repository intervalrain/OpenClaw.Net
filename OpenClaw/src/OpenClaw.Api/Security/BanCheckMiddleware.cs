using System.Text.Json;

using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Api.Security;

/// <summary>
/// Checks if the authenticated user is banned on every request.
/// Since JWT tokens are stateless, this middleware provides real-time ban enforcement
/// by querying the database for user status. Banned users receive 403 with ban reason.
/// </summary>
public class BanCheckMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check authenticated users
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst("id")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                var userRepository = context.RequestServices.GetRequiredService<IUserRepository>();
                var user = await userRepository.GetByIdAsync(userId);

                if (user?.Status == UserStatus.Banned)
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        code = "User.AccountBanned",
                        reason = user.BanReason ?? "Your account has been banned.",
                        banned = true
                    }, JsonOptions));
                    return;
                }
            }
        }

        await next(context);
    }
}
