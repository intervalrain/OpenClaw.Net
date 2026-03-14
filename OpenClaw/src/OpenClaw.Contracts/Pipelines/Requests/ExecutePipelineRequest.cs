using System.Text.Json;

namespace OpenClaw.Contracts.Pipelines.Requests;

public record ExecutePipelineRequest(
    JsonElement? Args = null);
