using ErrorOr;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Auth.Commands;

public record LoginCommand(
    string Email,
    string Password) : ICommand<ErrorOr<AuthResponse>>;
