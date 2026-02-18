using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Chat.Requests;
using OpenClaw.Contracts.Chat.Response;
using OpenClaw.Contracts.Llm;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Chat.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
public class ChatController(
    IAgentPipeline pipeline,
    IConversationRepository repository,
    ILlmProvider llmProvider,
    IUnitOfWork uow) : ApiController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request, CancellationToken ct)
    {
        var (conversation, history, isFirstMessage) = await LoadConversationAsync(request.ConversationId, ct);

        var response = await pipeline.ExecuteAsync(request.Message, history, request.Language, ct);

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

            await foreach (var evt in pipeline.ExecuteStreamAsync(request.Message, history, request.Language, ct))
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

    private async Task<List<ChatMessage>> CompactHistoryIfNeededAsync(List<ChatMessage> history, CancellationToken ct)
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

    private async Task<string> SummarizeConversationAsync(List<ChatMessage> messages, CancellationToken ct)
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
            var response = await llmProvider.ChatAsync(summaryMessages, ct: ct);
            return response.Content ?? "Previous conversation context.";
        }
        catch
        {
            // Fallback: just truncate
            return conversationText.Length > 500 ? conversationText[..500] + "..." : conversationText;
        }
    }

    private async Task<string> GenerateTitleAsync(string userMessage, string assistantResponse, CancellationToken ct)
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
}