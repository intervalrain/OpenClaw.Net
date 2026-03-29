using ErrorOr;

using OpenClaw.Contracts.Users.Dtos;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Users.Queries;

public record GetUsersQuery(string? StatusFilter = null) : IQuery<ErrorOr<List<UserDto>>>;
