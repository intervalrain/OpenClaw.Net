using Weda.Core.Application.Security;

using ErrorOr;
using Weda.Core.Application.Security.Models;

namespace OpenClaw.Infrastructure.Security.PolicyEnforcer;

public class PolicyEnforcer : IPolicyEnforcer
{
    public ErrorOr<Success> Authorize<T>(
        IAuthorizeableQuery<T> request,
        CurrentUser currentUser,
        string policy)
    {
        return policy switch
        {
            Policy.SelfOrAdmin => SelfOrAdminPolicy(request, currentUser),
            Policy.SuperAdminOnly => SuperAdminOnlyPolicy(currentUser),
            Policy.AdminOrAbove => AdminOrAbovePolicy(currentUser),
            _ => Error.Unexpected(description: "Unknown policy name"),
        };
    }

    private static ErrorOr<Success> SelfOrAdminPolicy<T>(IAuthorizeableQuery<T> request, CurrentUser currentUser) =>
        request.UserId == currentUser.Id || currentUser.Roles.Contains(Role.Admin) || currentUser.Roles.Contains(Role.SuperAdmin)
            ? Result.Success
            : Error.Unauthorized(description: "Requesting user failed policy requirement");

    private static ErrorOr<Success> SuperAdminOnlyPolicy(CurrentUser currentUser) =>
        currentUser.Roles.Contains(Role.SuperAdmin)
            ? Result.Success
            : Error.Unauthorized(description: "Only SuperAdmin can perform this action");

    private static ErrorOr<Success> AdminOrAbovePolicy(CurrentUser currentUser) =>
        currentUser.Roles.Contains(Role.Admin) || currentUser.Roles.Contains(Role.SuperAdmin)
            ? Result.Success
            : Error.Unauthorized(description: "Only Admin or SuperAdmin can perform this action");
}