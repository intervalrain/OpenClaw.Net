using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Application.Skills;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Chat.Requests;
using OpenClaw.Contracts.Chat.Responses;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Pipelines;
using OpenClaw.Contracts.Pipelines.Responses;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Chat.Controllers;

[ApiVersion("1.0")]
public class ChatController(
    IAgentPipeline pipeline,
    IConversationRepository repository,
    ILlmProviderFactory llmProviderFactory,
    ISlashCommandParser slashCommandParser,
    IToolRegistry skillRegistry,
    IToolSettingsService skillSettingsService,
    IEnumerable<IToolPipeline> skillPipelines,
    IPipelineExecutionStore pipelineExecutionStore,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork uow) : ApiController
{
    private readonly Dictionary<string, IToolPipeline> _pipelines = skillPipelines
        .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request, CancellationToken ct)
    {
        var (conversation, history, isFirstMessage) = await LoadConversationAsync(request.ConversationId, ct);

        // Convert image attachments to ImageContent
        var images = ConvertToImageContent(request.Images);

        var response = await pipeline.ExecuteAsync(request.Message, history, request.Language, images, ct);

        // Save messages to DB
        if (conversation != null)
        {
            conversation.AddMessage(ChatRole.User, request.Message);
            conversation.AddMessage(ChatRole.Assistant, response);

            if (isFirstMessage)
            {
                var title = await GenerateTitleAsync(request.Message, response, ct);
                conversation.UpdateTitle(title);
            }

            await uow.SaveChangesAsync(ct);
        }

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

        // Load conversation history
        var (conversation, history, isFirstMessage) = await LoadConversationAsync(request.ConversationId, ct);

        try
        {
            string assistantResponse = "";

            if (slashCommandParser.TryParse(request.Message, out var command))
            {
                // Check if this is a pipeline command (e.g., /pipeline:ado_task_sync or /ado-sync)
                if (TryHandlePipelineCommand(command!, out var pipelineName, out var argsJson))
                {
                    await ExecutePipelineStreamAsync(pipelineName, argsJson, ct);
                    return;
                }

                var skill = skillRegistry.GetSkill(command!.SkillName);
                if (skill == null)
                {
                    var availableSkills = string.Join(", ", skillRegistry.GetAllSkills().Select(s => s.Name));
                    var availablePipelines = string.Join(", ", _pipelines.Keys);
                    await WriteErrorEventAsync($"Skill '{command.SkillName}' not found. Available skills: {availableSkills}. Pipelines: {availablePipelines}", ct);
                    return;
                }

                if (!await skillSettingsService.IsEnabledAsync(command.SkillName, ct))
                {
                    await WriteErrorEventAsync($"Skill '{command.SkillName}' is disabled.", ct);
                    return;
                }

                // Execute skill first
                var jsonArgs = slashCommandParser.ConvertToJson(command, skill);
                var skillContext = new ToolContext(jsonArgs);
                var skillResult = await skill.ExecuteAsync(skillContext, ct);

                if (!skillResult.IsSuccess)
                {
                    await WriteErrorEventAsync($"Skill error: {skillResult.Error}", ct);
                    return;
                }

                // Inject skill result into history as tool call/result pair
                var toolCallId = Guid.NewGuid().ToString();
                history.Add(new ChatMessage(
                    ChatRole.Assistant,
                    Content: null,
                    ToolCalls: [new ToolCall(toolCallId, command.SkillName, jsonArgs)]));
                history.Add(new ChatMessage(ChatRole.Tool, skillResult.Output ?? "", toolCallId));
            }

            // Convert image attachments to ImageContent
            var images = ConvertToImageContent(request.Images);

            // Stream LLM response with history (including any injected skill results)
            var eventStream = pipeline.ExecuteStreamAsync(request.Message, history, request.Language, images, ct);

            await foreach (var evt in eventStream)
            {
                var data = JsonSerializer.Serialize(evt, JsonOptions);
                await Response.WriteAsync($"data: {data}\n\n", ct);
                await Response.Body.FlushAsync(ct);

                // Accumulate assistant response
                if (evt.Type == AgentStreamEventType.ContentDelta && evt.Content != null)
                {
                    assistantResponse += evt.Content;
                }
            }

            // Save messages directly to DB (EF Core tracks changes automatically)
            if (conversation != null)
            {
                conversation.AddMessage(ChatRole.User, request.Message);
                conversation.AddMessage(ChatRole.Assistant, assistantResponse);

                // Update title on first message using LLM summarization
                if (isFirstMessage)
                {
                    var title = await GenerateTitleAsync(request.Message, assistantResponse, ct);
                    conversation.UpdateTitle(title);
                }

                await uow.SaveChangesAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected, ignore
        }
    }

    private async Task WriteErrorEventAsync(string errorMessage, CancellationToken ct)
    {
        var evt = new AgentStreamEvent(AgentStreamEventType.Error, errorMessage);
        var data = JsonSerializer.Serialize(evt, JsonOptions);
        await Response.WriteAsync($"data: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task<(Conversation? conversation, List<ChatMessage> history, bool isFirstMessage)> LoadConversationAsync(
        Guid? conversationId,
        CancellationToken ct)
    {
        if (!conversationId.HasValue)
            return (null, [], false);

        var conversation = await repository.GetByIdAsync(conversationId.Value, ct);
        if (conversation == null)
            return (null, [], false);

        var history = conversation.Messages.Select(m => m.ToLlmMessage()).ToList();
        var isFirstMessage = conversation.Messages.Count == 0;

        // Compact history if too long
        history = await CompactHistoryIfNeededAsync(history, ct);

        return (conversation, history, isFirstMessage);
    }

    private const int MaxTokenEstimate = 4000; // ~4k tokens for context
    private const int RecentMessagesToKeep = 6; // Keep last 3 exchanges (6 messages)

    private async Task<List<ChatMessage>> CompactHistoryIfNeededAsync(
        List<ChatMessage> history,
        CancellationToken ct)
    {
        if (history.Count <= RecentMessagesToKeep)
            return history;

        // Estimate tokens (rough: 1 token ≈ 4 chars for English, 1.5 chars for Chinese)
        var totalChars = history.Sum(m => m.Content?.Length ?? 0);
        var estimatedTokens = totalChars / 2; // Conservative estimate

        if (estimatedTokens <= MaxTokenEstimate)
            return history;

        // Split into old messages (to summarize) and recent messages (to keep)
        var oldMessages = history.Take(history.Count - RecentMessagesToKeep).ToList();
        var recentMessages = history.Skip(history.Count - RecentMessagesToKeep).ToList();

        // Summarize old messages
        var summary = await SummarizeConversationAsync(oldMessages, ct);

        // Return summary + recent messages
        var compacted = new List<ChatMessage>
        {
            new(ChatRole.System, $"[Previous conversation summary]\n{summary}")
        };
        compacted.AddRange(recentMessages);

        return compacted;
    }

    private async Task<string> SummarizeConversationAsync(
        List<ChatMessage> messages,
        CancellationToken ct)
    {
        const string systemPrompt = """
            Summarize the following conversation concisely.
            Focus on key topics discussed, decisions made, and important context.
            Keep the summary under 500 characters.
            Use the same language as the conversation.
            """;

        var conversationText = string.Join("\n", messages.Select(m =>
            $"{(m.Role == ChatRole.User ? "User" : "Assistant")}: {m.Content}"));

        var summaryMessages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, conversationText)
        };

        try
        {
            var llmProvider = await llmProviderFactory.GetProviderAsync(ct);
            var response = await llmProvider.ChatAsync(summaryMessages, ct: ct);
            return response.Content ?? "Previous conversation context.";
        }
        catch
        {
            // Fallback: just truncate
            return conversationText.Length > 500 ? conversationText[..500] + "..." : conversationText;
        }
    }

    private async Task<string> GenerateTitleAsync(
        string userMessage,
        string assistantResponse,
        CancellationToken ct)
    {
        const string systemPrompt = """
            Generate a concise title (10-30 characters) for this conversation.
            Return ONLY the title, no quotes, no explanation.
            Use the same language as the user's message.
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"User: {userMessage}\nAssistant: {assistantResponse[..Math.Min(200, assistantResponse.Length)]}")
        };

        try
        {
            var llmProvider = await llmProviderFactory.GetProviderAsync(ct);
            var response = await llmProvider.ChatAsync(messages, ct: ct);
            var title = response.Content?.Trim() ?? userMessage[..Math.Min(30, userMessage.Length)];
            return title.Length > 50 ? title[..50] : title;
        }
        catch
        {
            // Fallback to truncation if LLM fails
            return userMessage.Length > 30 ? userMessage[..30] + "..." : userMessage;
        }
    }

    /// <summary>
    /// Converts chat image attachments to ImageContent for LLM processing.
    /// </summary>
    private static IReadOnlyList<ImageContent>? ConvertToImageContent(IReadOnlyList<ChatImageAttachment>? attachments)
    {
        if (attachments is not { Count: > 0 })
            return null;

        return attachments
            .Select(a => new ImageContent(a.Base64Data, a.MimeType))
            .ToList();
    }

    /// <summary>
    /// Check if slash command is a pipeline command.
    /// Formats: /pipeline:name, /ado-sync (alias for ado_task_sync)
    /// </summary>
    private bool TryHandlePipelineCommand(SlashCommand command, out string pipelineName, out string? argsJson)
    {
        pipelineName = "";
        argsJson = null;

        // Format 1: /pipeline:name args
        if (command.SkillName.StartsWith("pipeline:", StringComparison.OrdinalIgnoreCase))
        {
            pipelineName = command.SkillName[9..]; // Remove "pipeline:" prefix
            argsJson = string.IsNullOrWhiteSpace(command.RawArguments) ? null : command.RawArguments;
            return _pipelines.ContainsKey(pipelineName);
        }

        // Format 2: Alias mappings
        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ado-sync"] = "ado_task_sync",
            ["ado_sync"] = "ado_task_sync"
        };

        if (aliasMap.TryGetValue(command.SkillName, out var mappedName))
        {
            pipelineName = mappedName;
            argsJson = string.IsNullOrWhiteSpace(command.RawArguments) ? null : command.RawArguments;
            return _pipelines.ContainsKey(pipelineName);
        }

        // Format 3: Direct pipeline name
        if (_pipelines.ContainsKey(command.SkillName))
        {
            pipelineName = command.SkillName;
            argsJson = string.IsNullOrWhiteSpace(command.RawArguments) ? null : command.RawArguments;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Execute pipeline and stream events (including approval requests) to client.
    /// </summary>
    private async Task ExecutePipelineStreamAsync(string pipelineName, string? argsJson, CancellationToken ct)
    {
        if (!_pipelines.TryGetValue(pipelineName, out var skillPipeline))
        {
            await WriteErrorEventAsync($"Pipeline '{pipelineName}' not found.", ct);
            return;
        }

        // Create execution record
        var execution = await pipelineExecutionStore.CreateAsync(pipelineName, argsJson, ct);

        // Send initial "thinking" event
        await WriteEventAsync(new AgentStreamEvent(AgentStreamEventType.ToolExecuting, $"Starting pipeline: {pipelineName}", pipelineName), ct);

        try
        {
            // Get current user for pipeline context
            var userId = currentUserProvider.GetCurrentUser().Id;
            var context = new PipelineExecutionContext(userId, argsJson);

            // Run pipeline with approval callback
            var result = await skillPipeline.RunAsync(
                context,
                async approvalRequest =>
                {
                    // Convert ProposedChange to ProposedChangeInfo
                    var changeInfos = approvalRequest.ProposedChanges
                        .Select(c => new ProposedChangeInfo(
                            c.WorkItemId,
                            c.Title,
                            c.WorkItemType,
                            c.CurrentState,
                            c.ProposedState,
                            c.Reason,
                            c.RelatedCommits,
                            c.WorkItemUrl))
                        .ToList();

                    // Update execution status
                    await pipelineExecutionStore.SetPendingApprovalAsync(
                        execution.Id,
                        new PipelineApprovalInfo(approvalRequest.StepName, approvalRequest.Description, changeInfos),
                        ct);
                    await pipelineExecutionStore.UpdateStatusAsync(execution.Id, PipelineExecutionStatus.WaitingForApproval, ct);

                    // Send approval event to client
                    await WriteEventAsync(new AgentStreamEvent(
                        AgentStreamEventType.ApprovalRequired,
                        approvalRequest.Description,
                        approvalRequest.StepName,
                        approvalRequest,
                        execution.Id), ct);

                    // Wait for user decision
                    var approved = await pipelineExecutionStore.WaitForApprovalAsync(execution.Id, ct);

                    // Update status based on decision
                    await pipelineExecutionStore.UpdateStatusAsync(
                        execution.Id,
                        approved ? PipelineExecutionStatus.Running : PipelineExecutionStatus.Rejected,
                        ct);

                    return approved;
                },
                ct);

            // Store result
            await pipelineExecutionStore.SetResultAsync(execution.Id, result, ct);
            await pipelineExecutionStore.UpdateStatusAsync(
                execution.Id,
                result.IsSuccess ? PipelineExecutionStatus.Completed : PipelineExecutionStatus.Failed,
                ct);

            // Send completion event with summary
            await WriteEventAsync(new AgentStreamEvent(
                AgentStreamEventType.ToolCompleted,
                result.Summary,
                pipelineName), ct);

            // Send final content as markdown
            var summaryContent = FormatPipelineResult(result);
            await WriteEventAsync(new AgentStreamEvent(AgentStreamEventType.ContentDelta, summaryContent), ct);
            await WriteEventAsync(new AgentStreamEvent(AgentStreamEventType.Completed, summaryContent), ct);
        }
        catch (Exception ex)
        {
            await pipelineExecutionStore.UpdateStatusAsync(execution.Id, PipelineExecutionStatus.Failed, ct);
            await WriteErrorEventAsync($"Pipeline error: {ex.Message}", ct);
        }
    }

    private static string FormatPipelineResult(ToolPipelineResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## Pipeline Result: {(result.IsSuccess ? "Success" : "Failed")}");
        sb.AppendLine();
        sb.AppendLine(result.Summary);

        if (result.Steps.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Steps");
            foreach (var step in result.Steps)
            {
                var icon = step.IsSuccess ? "✓" : "✗";
                sb.AppendLine($"- {icon} **{step.StepName}**: {step.Output ?? (step.IsSuccess ? "OK" : step.Error)}");
            }
        }

        return sb.ToString();
    }

    private async Task WriteEventAsync(AgentStreamEvent evt, CancellationToken ct)
    {
        var data = JsonSerializer.Serialize(evt, JsonOptions);
        await Response.WriteAsync($"data: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}