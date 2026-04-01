using ErrorOr;

using ClawOS.Contracts.Users.Dtos;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Contracts.Users.Queries;

public record ListUserPreferencesQuery(string? Prefix = null) : ICommand<ErrorOr<List<UserPreferenceDto>>>;
