using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.CronJobs.Commands;

/// <summary>
/// Command to trigger cron job execution.
/// Returns the execution ID immediately; execution runs in background.
/// </summary>
public record ExecuteCronJobCommand(
    Guid Id,
    Guid? UserId) : IRequest<ErrorOr<Guid>>;
