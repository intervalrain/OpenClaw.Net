using ErrorOr;

using ClawOS.Contracts.Auth.Responses;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Contracts.Auth.Commands;

public record RegisterCommand(
    string Email,
    string Password,
    string Name) : ICommand<ErrorOr<RegisterResponse>>;
