using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using Throw;

using Weda.Core.Application.Security;

using Weda.Core.Application.Security.Models;

namespace OpenClaw.Infrastructure.Security.CurrentUserProvider;

public class CurrentUserProvider(IHttpContextAccessor _httpContextAccessor) : ICurrentUserProvider
{
    public CurrentUser GetCurrentUser()
    {
        _httpContextAccessor.HttpContext.ThrowIfNull();

        var id = Guid.Parse(GetSingleClaimValue("id"));
        var permissions = GetClaimValues("permissions");
        var roles = GetClaimValues(ClaimTypes.Role);
        var name = GetSingleClaimValue(JwtRegisteredClaimNames.Name);
        var email = GetSingleClaimValue(ClaimTypes.Email);

        return new CurrentUser(id, name, email, permissions, roles);
    }

    private List<string> GetClaimValues(string claimType) =>
        _httpContextAccessor.HttpContext!.User.Claims
            .Where(claim => claim.Type == claimType)
            .Select(claim => claim.Value)
            .ToList();

    private string GetSingleClaimValue(string claimType) =>
        _httpContextAccessor.HttpContext!.User.Claims
            .Single(claim => claim.Type == claimType)
            .Value;
}