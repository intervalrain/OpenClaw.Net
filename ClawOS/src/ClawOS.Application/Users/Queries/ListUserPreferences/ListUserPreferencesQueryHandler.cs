using ErrorOr;

using Mediator;

using ClawOS.Contracts.Users.Dtos;
using ClawOS.Contracts.Users.Queries;
using ClawOS.Domain.Users.Repositories;

using Weda.Core.Application.Security;

namespace ClawOS.Application.Users.Queries.ListUserPreferences;

public class ListUserPreferencesQueryHandler(
    IUserPreferenceRepository repository,
    ICurrentUserProvider currentUserProvider) : IRequestHandler<ListUserPreferencesQuery, ErrorOr<List<UserPreferenceDto>>>
{
    public async ValueTask<ErrorOr<List<UserPreferenceDto>>> Handle(ListUserPreferencesQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetCurrentUser().Id;

        var preferences = string.IsNullOrWhiteSpace(request.Prefix)
            ? await repository.GetAllByUserAsync(userId, ct)
            : await repository.GetByPrefixAsync(userId, request.Prefix, ct);

        return preferences
            .Select(p => new UserPreferenceDto(p.Key, p.Value))
            .ToList();
    }
}
