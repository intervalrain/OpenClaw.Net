using ErrorOr;

using OpenClaw.Contracts.Auth.Responses;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Auth.Commands;

public record VerifyRegistrationCommand(string Email, string Code) : ICommand<ErrorOr<VerifyRegistrationResponse>>;