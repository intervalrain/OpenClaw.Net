using ErrorOr;

using ClawOS.Contracts.Users.Dtos;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Contracts.Users.Queries;

public record GetUsersQuery(string? StatusFilter = null) : IQuery<ErrorOr<List<UserDto>>>;
