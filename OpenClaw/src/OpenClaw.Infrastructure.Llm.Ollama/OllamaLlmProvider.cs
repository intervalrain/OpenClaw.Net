using System.Text.Json;

using OllamaSharp;
using OllamaSharp.Models.Chat;

using OpenClaw.Contracts.Llm;

namespace OpenClaw.Infrastructure.Llm.Ollama;

public class OllamaLlmProvider(OllamaApiClient client, string model) : ILlmProvider
{
    public string Name => $"ollama:{model}";

    public async Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition>? tools = null, CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            Model = model,
            Messages = messages.Select(ToOllamaMessage).ToList(),
            Tools = tools?.Select(ToOllamaTool).ToList(),
        };

        ChatResponseStream? response = null;
        await foreach (var chunk in client.ChatAsync(request, ct))
        {
            response = chunk;
        }

        return ToChatResponse(response);
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
            Parameters = tool.Parameters as Parameters,
        },
    };

    private static ChatResponse ToChatResponse(ChatResponseStream? response)
    {
        var content = response?.Message?.Content;
        var toolCalls = response?.Message?.ToolCalls?
            .Select(tc => new ToolCall(
                Guid.NewGuid().ToString(),
                tc.Function?.Name ?? "",
                JsonSerializer.Serialize(tc.Function?.Arguments)))
            .ToList();
        
        return new ChatResponse(content, toolCalls);
    }
}