using ErrorOr;
using Mediator;
using ClawOS.Contracts.CronJobs.Responses;

namespace ClawOS.Contracts.CronJobs.Commands;

public record CreateToolInstanceCommand(
    string Name,
    string ToolName,
    string? ArgsJson,
    string? Description,
    Guid UserId) : IRequest<ErrorOr<ToolInstanceResponse>>;
