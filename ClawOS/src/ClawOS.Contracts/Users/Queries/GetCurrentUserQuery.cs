using ErrorOr;
using Weda.Core.Application.Interfaces;
using ClawOS.Contracts.Users.Dtos;

namespace ClawOS.Contracts.Users.Queries;

public record GetCurrentUserQuery : IQuery<ErrorOr<UserDto>>;
