using ErrorOr;

using ClawOS.Contracts.Auth.Responses;


using Weda.Core.Application.Interfaces;

namespace ClawOS.Contracts.Auth.Commands;

public record LoginCommand(
    string Email,
    string Password) : ICommand<ErrorOr<AuthResponse>>;
