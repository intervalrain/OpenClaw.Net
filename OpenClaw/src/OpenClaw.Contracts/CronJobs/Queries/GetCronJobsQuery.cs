using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Responses;

namespace OpenClaw.Contracts.CronJobs.Queries;

/// <summary>
/// Query to list all cron jobs, optionally filtered by user and active status.
/// </summary>
public record GetCronJobsQuery(
    Guid? UserId = null,
    bool? IsActive = null) : IRequest<ErrorOr<IReadOnlyList<CronJobResponse>>>;

/// <summary>
/// Query to get a specific cron job by ID. UserId is required for ownership validation.
/// </summary>
public record GetCronJobQuery(
    Guid Id,
    Guid UserId) : IRequest<ErrorOr<CronJobResponse>>;

/// <summary>
/// Query to list executions for a cron job. UserId is required for ownership validation.
/// </summary>
public record GetCronJobExecutionsQuery(
    Guid UserId,
    Guid? CronJobId,
    int Limit = 20,
    int Offset = 0) : IRequest<ErrorOr<IReadOnlyList<CronJobExecutionResponse>>>;

/// <summary>
/// Query to list tool instances, optionally filtered by user.
/// </summary>
public record GetToolInstancesQuery(
    Guid? UserId = null) : IRequest<ErrorOr<IReadOnlyList<ToolInstanceResponse>>>;
