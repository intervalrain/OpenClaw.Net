using ErrorOr;

using OpenClaw.Contracts.Auth.Responses;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Auth.Commands;

public record InitiateRegistrationCommand(
    string Email,
    string Password,
    string Name) : ICommand<ErrorOr<InitiateRegistrationResponse>>;