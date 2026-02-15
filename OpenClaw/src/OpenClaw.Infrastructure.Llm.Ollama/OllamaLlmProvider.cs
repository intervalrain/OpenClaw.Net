using System.Text;
using System.Text.Json;

using OllamaSharp;

using OllamaSharp.Models.Chat;

using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;

namespace OpenClaw.Infrastructure.Llm.Ollama;

public class OllamaLlmProvider(IConfigStore config) : ILlmProvider
{
    public string Name => $"ollama:{_model}";
    private readonly OllamaApiClient _client = new(config.Get(ConfigKeys.OllamaUrl) ?? "http://localhost:11434");
    private readonly string _model = config.Get(ConfigKeys.OllamaModel) ?? "qwen2.5:7b";

    public async Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools = null, CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            Model = _model,
            Messages = messages.Select(ToOllamaMessage).ToList(),
            Tools = tools?.Select(ToOllamaTool).ToList(),
        };

        var sb = new StringBuilder();
        List<Message.ToolCall>? toolCalls = null;

        await foreach (var chunk in _client.ChatAsync(request, ct))
        {
            if (chunk?.Message.Content is { } content)
            {
                sb.Append(content);
            }

            if (chunk?.Message.ToolCalls is { } tc && tc.Any())
            {
                toolCalls = chunk.Message.ToolCalls.ToList();   
            }
        }

        return ToChatResponse(sb.ToString(), toolCalls);
    }

    public IAsyncEnumerable<ChatResponseChunk> ChatStreamAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    private static Message ToOllamaMessage(ChatMessage msg) => new()
    {
        Role = msg.Role switch
        {
            Contracts.Llm.ChatRole.System => OllamaSharp.Models.Chat.ChatRole.System,
            Contracts.Llm.ChatRole.User => OllamaSharp.Models.Chat.ChatRole.User,
            Contracts.Llm.ChatRole.Assistant => OllamaSharp.Models.Chat.ChatRole.Assistant,
            Contracts.Llm.ChatRole.Tool => OllamaSharp.Models.Chat.ChatRole.Tool,
            _ => OllamaSharp.Models.Chat.ChatRole.User,
        },
        Content = msg.Content,
    };

    private static Tool ToOllamaTool(ToolDefinition tool) => new()
    {
        Function = new Function
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = ConvertToParameters(tool.Parameters),
        },
    };

    private static Parameters? ConvertToParameters(object? parameters)
    {
        if (parameters is not ToolParameters toolParams) return null;

        return new Parameters
        {
            Type = toolParams.Type,
            Required = toolParams.Required,
            Properties = toolParams.Properties?.ToDictionary(
                kvp => kvp.Key,
                kvp => new Property
                {
                    Type = kvp.Value.Type,
                    Description = kvp.Value.Description
                })
        };
    }

    private static ChatResponse ToChatResponse(string content, List<Message.ToolCall>? toolCalls)
    {
        var mappedToolCalls = toolCalls?
            .Select(tc => new ToolCall(
                Guid.NewGuid().ToString(),
                tc.Function?.Name ?? "",
                JsonSerializer.Serialize(tc.Function?.Arguments)))
            .ToList();

        return new ChatResponse(
            string.IsNullOrEmpty(content) ? null : content,
            mappedToolCalls);
    }
}