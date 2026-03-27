using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Responses;

namespace OpenClaw.Contracts.CronJobs.Commands;

public record UpdateToolInstanceCommand(
    Guid Id,
    string? Name,
    string? ToolName,
    string? ArgsJson,
    string? Description,
    Guid UserId) : IRequest<ErrorOr<ToolInstanceResponse>>;
