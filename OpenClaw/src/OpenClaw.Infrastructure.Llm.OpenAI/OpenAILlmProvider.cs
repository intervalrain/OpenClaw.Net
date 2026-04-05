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

public class OpenAILlmProvider : ILlmProvider
{
    public string Name => "OpenAI";
    public int MaxContextTokens { get; }

    private readonly OpenAIClient _client;
    private readonly string _model;

    public OpenAILlmProvider(string apiKey, string model, int? maxContextTokens = null)
    {
        _client = new OpenAIClient(apiKey);
        _model = model;
        MaxContextTokens = maxContextTokens ?? LookupContextWindow(model);
    }

    /// <summary>
    /// Hardcode fallback for known OpenAI models (updated 2026-04).
    /// Overridden when user/admin sets MaxContextTokens in DB.
    /// Source: https://github.com/taylorwilsdon/llm-context-limits
    /// </summary>
    private static int LookupContextWindow(string model) => model.ToLowerInvariant() switch
    {
        // GPT-5.4 series
        var m when m.Contains("gpt-5.4") && !m.Contains("mini") && !m.Contains("nano") => 1_050_000,
        var m when m.Contains("gpt-5.4-mini") || m.Contains("gpt-5.4-nano") => 400_000,
        // GPT-5.x series (5.0 / 5.1 / 5.2 / codex)
        var m when m.Contains("gpt-5") => 400_000,
        // GPT-4.1 series
        var m when m.Contains("gpt-4.1") => 1_047_576,
        // GPT-4o series
        var m when m.Contains("gpt-4o") => 128_000,
        // GPT-4 legacy
        var m when m.Contains("gpt-4-turbo") => 128_000,
        var m when m.Contains("gpt-4") => 8_192,
        // GPT-3.5
        var m when m.Contains("gpt-3.5") => 16_385,
        // o-series reasoning models
        var m when m.Contains("o4-mini") => 200_000,
        var m when m.Contains("o3") => 200_000,
        var m when m.Contains("o1-mini") => 128_000,
        var m when m.Contains("o1") => 200_000,
        // Unknown — conservative default
        _ => 128_000
    };

    public OpenAILlmProvider(IConfigStore config)
        : this(
            config.GetRequired(ConfigKeys.OpenAiApiKey), 
            config.Get(ConfigKeys.OpenAiModel) ?? "gpt-4o-mini")
    {
    }

    public async Task<LlmChatResponse> ChatAsync(
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

                var usage = update.Usage is not null
                    ? new LlmUsage(
                        InputTokens: update.Usage.InputTokenCount,
                        OutputTokens: update.Usage.OutputTokenCount)
                    : null;

                yield return new ChatResponseChunk(IsComplete: true, Usage: usage);
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
                // Check if message contains images for Vision API
                if (msg.HasImages)
                {
                    var contentParts = new List<ChatMessageContentPart>();

                    // Add images first
                    foreach (var image in msg.Images!)
                    {
                        // Create data URI for base64 image
                        var dataUri = $"data:{image.MimeType};base64,{image.Base64Data}";
                        contentParts.Add(ChatMessageContentPart.CreateImagePart(new Uri(dataUri)));
                    }

                    // Add text content if present
                    if (!string.IsNullOrEmpty(msg.Content))
                    {
                        contentParts.Add(ChatMessageContentPart.CreateTextPart(msg.Content));
                    }

                    return new UserChatMessage(contentParts);
                }
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

    private static LlmChatResponse ToChatResponse(ChatCompletion completion)
    {
        var toolCalls = completion.ToolCalls?
            .Select(tc => new ToolCall(tc.Id, tc.FunctionName, tc.FunctionArguments.ToString()))
            .ToList();

        var usage = completion.Usage is not null
            ? new LlmUsage(
                InputTokens: completion.Usage.InputTokenCount,
                OutputTokens: completion.Usage.OutputTokenCount)
            : null;

        return new LlmChatResponse(
            completion.Content?.FirstOrDefault()?.Text,
            toolCalls,
            usage);
    }
}