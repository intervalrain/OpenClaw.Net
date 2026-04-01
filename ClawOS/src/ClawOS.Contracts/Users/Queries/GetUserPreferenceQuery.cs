using ErrorOr;

using ClawOS.Contracts.Users.Dtos;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Contracts.Users.Queries;

public record GetUserPreferenceQuery(string Key) : ICommand<ErrorOr<UserPreferenceDto?>>;
