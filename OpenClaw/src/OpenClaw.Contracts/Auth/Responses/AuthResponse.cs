using Swashbuckle.AspNetCore.Filters;

namespace OpenClaw.Contracts.Auth;

public record AuthResponse(
    string Token,
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Roles);

public class AuthResponseExample : IExamplesProvider<AuthResponse>
{
    public AuthResponse GetExamples() => new(
        Token: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        Id: Guid.Parse("6DE4C12D-D70A-4C2F-88A3-E6DB8630AC5D"),
        Name: "John Doe",
        Email: "john.doe@example.com",
        Permissions: [],
        Roles: ["User"]);
}