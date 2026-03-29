using ErrorOr;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Users.Commands;

public record DeleteUserPreferenceCommand(string Key) : ICommand<ErrorOr<Success>>;
