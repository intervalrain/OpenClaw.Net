using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Chat.Requests;
using OpenClaw.Contracts.Chat.Response;

using Weda.Core.Presentation;

namespace OpenClaw.Api.Chat.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
public class ChatController(IAgentPipeline pipeline) : ApiController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request, CancellationToken ct)
    {
        var response = await pipeline.ExecuteAsync(request.Message, ct);
        return Ok(new ChatResponse(response));
    }

    [HttpPost("stream")]
    public async Task StreamMessage([FromBody] ChatRequest request, CancellationToken ct)
    {
        // Disable response buffering for SSE
        var bufferingFeature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
        bufferingFeature?.DisableBuffering();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // For nginx proxy

        try
        {
            await foreach (var evt in pipeline.ExecuteStreamAsync(request.Message, ct))
            {
                var data = JsonSerializer.Serialize(evt, JsonOptions);
                await Response.WriteAsync($"data: {data}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected, ignore
        }
    }
}