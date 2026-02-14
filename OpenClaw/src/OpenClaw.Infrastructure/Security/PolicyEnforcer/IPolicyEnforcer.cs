using Weda.Core.Application.Security;

using ErrorOr;
using Weda.Core.Application.Security.Models;

namespace OpenClaw.Infrastructure.Security.PolicyEnforcer;

public interface IPolicyEnforcer
{
    public ErrorOr<Success> Authorize<T>(
        IAuthorizeableQuery<T> request,
        CurrentUser currentUser,
        string policy);
}