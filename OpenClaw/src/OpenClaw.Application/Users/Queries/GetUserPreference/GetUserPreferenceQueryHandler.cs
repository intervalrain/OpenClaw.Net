using ErrorOr;

using Mediator;

using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Contracts.Users.Queries;
using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Security;

namespace OpenClaw.Application.Users.Queries.GetUserPreference;

public class GetUserPreferenceQueryHandler(
    IUserPreferenceRepository repository,
    ICurrentUserProvider currentUserProvider) : IRequestHandler<GetUserPreferenceQuery, ErrorOr<UserPreferenceDto?>>
{
    public async ValueTask<ErrorOr<UserPreferenceDto?>> Handle(GetUserPreferenceQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetCurrentUser().Id;
        var preference = await repository.GetByKeyAsync(userId, request.Key, ct);

        if (preference is null)
            return (UserPreferenceDto?)null;

        return new UserPreferenceDto(preference.Key, preference.Value);
    }
}
