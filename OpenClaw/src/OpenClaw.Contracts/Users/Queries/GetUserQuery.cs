using ErrorOr;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using OpenClaw.Contracts.Users.Dtos;

namespace OpenClaw.Contracts.Users.Queries;

[Authorize(Policies = Policy.SelfOrAdmin)]
public record GetUserQuery(Guid Id) : IAuthorizeableQuery<ErrorOr<UserDto>>
{
    public Guid UserId => Id;
}
