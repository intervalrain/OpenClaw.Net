using OpenClaw.Contracts.Configuration.Dtos;

namespace OpenClaw.Contracts.Configuration.Responses;

public record OllamaTagsResponse(List<OllamaModel> Models);