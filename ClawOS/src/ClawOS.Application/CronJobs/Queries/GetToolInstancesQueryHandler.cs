using ErrorOr;
using Mediator;
using ClawOS.Application.CronJobs.Commands;
using ClawOS.Contracts.CronJobs.Queries;
using ClawOS.Contracts.CronJobs.Responses;
using ClawOS.Domain.CronJobs.Repositories;

namespace ClawOS.Application.CronJobs.Queries;

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
