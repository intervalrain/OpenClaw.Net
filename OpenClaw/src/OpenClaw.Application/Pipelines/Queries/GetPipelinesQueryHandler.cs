using Mediator;

using OpenClaw.Contracts.Pipelines.Queries;
using OpenClaw.Contracts.Pipelines.Responses;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Pipelines.Queries;

public class GetPipelinesQueryHandler(
    IEnumerable<ISkillPipeline> pipelines) : IRequestHandler<GetPipelinesQuery, PipelineListResponse>
{
    public ValueTask<PipelineListResponse> Handle(GetPipelinesQuery query, CancellationToken ct)
    {
        var list = pipelines
            .Select(p => new PipelineInfoResponse(p.Name, p.Description, p.Parameters))
            .ToList();

        return ValueTask.FromResult(new PipelineListResponse(list));
    }
}