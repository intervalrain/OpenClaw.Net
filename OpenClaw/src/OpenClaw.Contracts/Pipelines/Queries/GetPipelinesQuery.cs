using Mediator;

using OpenClaw.Contracts.Pipelines.Responses;

namespace OpenClaw.Contracts.Pipelines.Queries;

public record GetPipelinesQuery : IRequest<PipelineListResponse>;