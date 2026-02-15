using OpenAI;
using OpenAI.Chat;

using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;

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

    public IAsyncEnumerable<ChatResponseChunk> ChatStreamAsync(
        IReadOnlyList<OpenClawChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
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