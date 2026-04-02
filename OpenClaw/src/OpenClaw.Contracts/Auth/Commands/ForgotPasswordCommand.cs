using ErrorOr;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Auth.Commands;

public record ForgotPasswordCommand(string Email, string BaseUrl) : ICommand<ErrorOr<ForgotPasswordResponse>>;

public record ForgotPasswordResponse(string Message);
