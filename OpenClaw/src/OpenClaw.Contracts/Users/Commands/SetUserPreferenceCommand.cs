using ErrorOr;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Users.Commands;

public record SetUserPreferenceCommand(
    string Key,
    string? Value) : ICommand<ErrorOr<Success>>;
