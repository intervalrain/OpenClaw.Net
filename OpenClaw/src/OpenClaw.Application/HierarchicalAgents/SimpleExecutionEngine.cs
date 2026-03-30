using System.Text.Json;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Simple execution engine that runs an LLM tool-use loop (same pattern as existing CronJobExecutor).
/// This is the "simple loop" strategy of IExecutionEngine.
/// </summary>
public class SimpleExecutionEngine(
    ILlmProviderFactory llmProviderFactory,
    IToolRegistry toolRegistry) : IExecutionEngine
{
    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct = default)
    {
        var llmProvider = request.UserId.HasValue
            ? await llmProviderFactory.GetProviderAsync(request.UserId.Value, ct: ct)
            : await llmProviderFactory.GetProviderAsync(ct);

        // Resolve tools
        var toolDefs = new List<ToolDefinition>();
        var toolMap = new Dictionary<string, IAgentTool>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in request.ToolNames)
        {
            var tool = toolRegistry.GetSkill(name);
            if (tool is not null && !toolMap.ContainsKey(tool.Name))
            {
                toolDefs.Add(new ToolDefinition(tool.Name, tool.Description, tool.Parameters));
                toolMap[tool.Name] = tool;
            }
        }

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(request.SystemPrompt))
            messages.Add(new ChatMessage(ChatRole.System, request.SystemPrompt));
        messages.Add(new ChatMessage(ChatRole.User, request.Content));

        var toolCallLog = new List<object>();

        for (var i = 0; i < request.MaxIterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            var response = await llmProvider.ChatAsync(messages, toolDefs.Count > 0 ? toolDefs : null, ct);

            if (!response.HasToolCalls)
            {
                return ExecutionResult.Success(
                    response.Content ?? "",
                    toolCallLog.Count > 0 ? JsonSerializer.Serialize(toolCallLog) : null);
            }

            messages.Add(new ChatMessage(ChatRole.Assistant, response.Content, ToolCalls: response.ToolCalls));

            foreach (var toolCall in response.ToolCalls!)
            {
                string toolResult;
                if (toolMap.TryGetValue(toolCall.Name, out var tool))
                {
                    var result = await tool.ExecuteAsync(new ToolContext(toolCall.Arguments)
                    {
                        UserId = request.UserId,
                        WorkspaceId = request.WorkspaceId
                    }, ct);
                    toolResult = result.IsSuccess ? result.Output ?? "" : $"Error: {result.Error}";
                }
                else
                {
                    toolResult = $"Error: Tool '{toolCall.Name}' not available";
                }

                toolCallLog.Add(new { tool = toolCall.Name, args = toolCall.Arguments, result = toolResult });
                messages.Add(new ChatMessage(ChatRole.Tool, toolResult, toolCall.Id));
            }
        }

        var lastContent = messages.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Content
            ?? "Max iterations reached";
        return ExecutionResult.Success(lastContent, JsonSerializer.Serialize(toolCallLog));
    }
}
