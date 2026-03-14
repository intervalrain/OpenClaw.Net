namespace OpenClaw.Contracts.Pipelines.Responses;

public record PipelineInfoResponse(
    string Name,
    string Description,
    object? Parameters);

public record PipelineListResponse(
    IReadOnlyList<PipelineInfoResponse> Pipelines);
