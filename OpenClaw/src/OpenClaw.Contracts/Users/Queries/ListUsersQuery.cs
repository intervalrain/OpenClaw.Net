using ErrorOr;
using Weda.Core.Application.Interfaces;
using OpenClaw.Contracts.Users.Dtos;

namespace OpenClaw.Contracts.Users.Queries;

public record ListUsersQuery : IQuery<ErrorOr<List<UserDto>>>;
