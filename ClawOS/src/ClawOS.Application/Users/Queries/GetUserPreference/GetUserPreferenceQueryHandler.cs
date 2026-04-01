using ErrorOr;

using Mediator;

using ClawOS.Contracts.Users.Dtos;
using ClawOS.Contracts.Users.Queries;
using ClawOS.Domain.Users.Repositories;

using Weda.Core.Application.Security;

namespace ClawOS.Application.Users.Queries.GetUserPreference;

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
