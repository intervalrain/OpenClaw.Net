using System.Runtime.CompilerServices;
using System.Text;
using OpenAI;
using OpenAI.Chat;

using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;
using OpenClaw.Domain.Chat.Enums;

using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using OpenClawChatMessage = OpenClaw.Contracts.Llm.ChatMessage;

namespace OpenClaw.Infrastructure.Llm.OpenAI;

public class OpenAILlmProvider(IConfigStore config) : ILlmProvider
{
    public string Name => "OpenAI";
    private readonly OpenAIClient _client = new(config.GetRequired(ConfigKeys.OpenAiApiKey));
    private readonly string _model = config.Get(ConfigKeys.OpenAiModel) ?? "gpt-4o-mini";

    public async Task<ChatResponse> ChatAsync(
        IReadOnlyList<OpenClawChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        CancellationToken ct = default)
    {
        var chatClient = _client.GetChatClient(_model);
        var options = new ChatCompletionOptions();

        if (tools is { Count: > 0 })
        {
            foreach (var tool in tools)
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description,
                    BinaryData.FromObjectAsJson(tool.Parameters)));
            }
        }

        var chatMessages = messages.Select(ToOpenAIMessage).ToList();
        var response = await chatClient.CompleteChatAsync(chatMessages, options, ct);

        return ToChatResponse(response.Value);
    }

    public async IAsyncEnumerable<ChatResponseChunk> ChatStreamAsync(
        IReadOnlyList<OpenClawChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var chatClient = _client.GetChatClient(_model);
        var options = new ChatCompletionOptions();

        if (tools is { Count: > 0 })
        {
            foreach (var tool in tools)
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description,
                    BinaryData.FromObjectAsJson(tool.Parameters)));
            }
        }

        var chatMessages = messages.Select(ToOpenAIMessage).ToList();

        // Track tool calls being built across chunks
        var toolCallBuilders = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

        await foreach (var update in chatClient.CompleteChatStreamingAsync(chatMessages, options, ct))
        {
            // Handle content delta
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return new ChatResponseChunk(ContentDelta: contentPart.Text);
                }
            }

            // Handle tool call updates
            foreach (var toolCallUpdate in update.ToolCallUpdates)
            {
                if (!toolCallBuilders.TryGetValue(toolCallUpdate.Index, out var builder))
                {
                    // ToolCallId comes in the first chunk for each tool call
                    var id = toolCallUpdate.ToolCallId ?? $"call_{Guid.NewGuid():N}";
                    builder = (id, toolCallUpdate.FunctionName ?? "", new StringBuilder());
                    toolCallBuilders[toolCallUpdate.Index] = builder;
                }
                else if (!string.IsNullOrEmpty(toolCallUpdate.FunctionName))
                {
                    // Update function name if it comes in a later chunk
                    builder = (builder.Id, toolCallUpdate.FunctionName, builder.Args);
                    toolCallBuilders[toolCallUpdate.Index] = builder;
                }

                // Accumulate function arguments
                var argsUpdate = toolCallUpdate.FunctionArgumentsUpdate?.ToString();
                if (!string.IsNullOrEmpty(argsUpdate))
                {
                    builder.Args.Append(argsUpdate);
                    toolCallBuilders[toolCallUpdate.Index] = builder;
                }
            }

            // Check if stream is complete
            if (update.FinishReason.HasValue)
            {
                // Emit completed tool calls
                foreach (var kvp in toolCallBuilders.OrderBy(x => x.Key))
                {
                    var (id, name, args) = kvp.Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        yield return new ChatResponseChunk(
                            ToolCall: new ToolCall(id, name, args.ToString()));
                    }
                }

                yield return new ChatResponseChunk(IsComplete: true);
            }
        }
    }

    private static OpenAIChatMessage ToOpenAIMessage(OpenClawChatMessage msg)
    {
        switch (msg.Role)
        {
            case ChatRole.System:
                return new SystemChatMessage(msg.Content);
            case ChatRole.User:
                return new UserChatMessage(msg.Content);
            case ChatRole.Assistant:
                if (msg.ToolCalls is { Count: > 0 })
                {
                    var assistantMsg = new AssistantChatMessage(msg.Content ?? "");
                    foreach (var tc in msg.ToolCalls)
                    {
                        assistantMsg.ToolCalls.Add(ChatToolCall.CreateFunctionToolCall(tc.Id, tc.Name, BinaryData.FromString(tc.Arguments)));
                    }
                    return assistantMsg;
                }
                return new AssistantChatMessage(msg.Content);
            case ChatRole.Tool:
                return new ToolChatMessage(msg.ToolCallId!, msg.Content ?? "");
            default:
                return new UserChatMessage(msg.Content);
        }
    }

    private static ChatResponse ToChatResponse(ChatCompletion completion)
    {
        var toolCalls = completion.ToolCalls?
            .Select(tc => new ToolCall(tc.Id, tc.FunctionName, tc.FunctionArguments.ToString()))
            .ToList();

        return new ChatResponse(
            completion.Content?.FirstOrDefault()?.Text,
            toolCalls);
    }
}