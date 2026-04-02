using ErrorOr;

using OpenClaw.Contracts.Auth.Responses;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Auth.Commands;

public record ResendVerificationCommand(string Email) : ICommand<ErrorOr<ResendVerificationResponse>>;