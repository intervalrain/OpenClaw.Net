using ErrorOr;
using Mediator;
using OpenClaw.Application.CronJobs.Commands;
using OpenClaw.Contracts.CronJobs.Queries;
using OpenClaw.Contracts.CronJobs.Responses;
using OpenClaw.Domain.CronJobs.Repositories;

namespace OpenClaw.Application.CronJobs.Queries;

public class GetToolInstancesQueryHandler(
    IToolInstanceRepository repository) : IRequestHandler<GetToolInstancesQuery, ErrorOr<IReadOnlyList<ToolInstanceResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<ToolInstanceResponse>>> Handle(
        GetToolInstancesQuery request,
        CancellationToken ct)
    {
        if (!request.UserId.HasValue)
        {
            return Error.Validation("UserId is required to list tool instances");
        }

        var instances = await repository.GetAllByUserAsync(request.UserId.Value, ct);

        var responses = instances
            .Select(CreateToolInstanceCommandHandler.ToResponse)
            .ToList();

        return responses;
    }
}
