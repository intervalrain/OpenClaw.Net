using ErrorOr;

using Mediator;

namespace OpenClaw.Contracts.Pipelines.Commands;

public record ExecutePipelineCommand(
    string PipelineName,
    string? ArgsJson = null,
    Guid? UserId = null) : IRequest<ErrorOr<string>>;
