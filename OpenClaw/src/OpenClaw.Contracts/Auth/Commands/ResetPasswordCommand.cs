using ErrorOr;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Auth.Commands;

public record ResetPasswordCommand(string Token, string NewPassword) : ICommand<ErrorOr<ResetPasswordResponse>>;

public record ResetPasswordResponse(string Message);
