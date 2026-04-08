using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Application.AgentActivities;
using OpenClaw.Application.Skills;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Chat.Requests;
using OpenClaw.Contracts.Chat.Responses;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.AgentActivities;
using OpenClaw.Domain.Agents.Repositories;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Chat.Repositories;

using OpenClaw.Contracts.Workspaces;
using OpenClaw.Tools.FileSystem;
using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Chat.Controllers;

[ApiVersion("1.0")]
public class ChatController(
    IAgentPipeline pipeline,
    IConversationRepository repository,
    ICurrentUserProvider currentUserProvider,
    ILlmProviderFactory llmProviderFactory,
    IChatSyntaxParser chatSyntaxParser,
    IAgentActivityTracker activityTracker,
    ICurrentWorkspaceProvider currentWorkspaceProvider,
    IContextCompressor contextCompressor,
    IAgentDefinitionRepository agentDefinitionRepo,
    IToolRegistry toolRegistry,
    IToolInstanceResolver toolInstanceResolver,
    IUnitOfWork uow) : ApiController
{
    private Guid GetUserId() => currentUserProvider.GetCurrentUser().Id;
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

        var userId = GetUserId();
        var currentUser = currentUserProvider.GetCurrentUser();
        var response = await pipeline.ExecuteAsync(request.Message, history, request.Language, images, userId, currentWorkspaceProvider.WorkspaceId, currentUser.Roles, ct);

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
            List<string>? priorityTools = null;

            var syntaxResult = chatSyntaxParser.Parse(request.Message);
            switch (syntaxResult)
            {
                case AgentInvokeResult agentInvoke:
                {
                    // // mounts an agent (system prompt + tool set)
                    var agent = await agentDefinitionRepo.GetByNameAsync(agentInvoke.AgentName, ct);
                    if (agent == null)
                    {
                        var availableAgents = (await agentDefinitionRepo.GetAllAsync(ct)).Select(a => a.Name);
                        await WriteErrorEventAsync($"Agent '{agentInvoke.AgentName}' not found. Available: {string.Join(", ", availableAgents)}", ct);
                        return;
                    }

                    history.Insert(0, new ChatMessage(
                        ChatRole.System,
                        $"[Mounted Agent: {agent.Name}]\n\n{agent.SystemPrompt}"));

                    request = request with { Message = string.IsNullOrWhiteSpace(agentInvoke.RawArguments)
                        ? $"Execute the task defined by the {agent.Name} agent."
                        : agentInvoke.RawArguments };
                    break;
                }

                case ToolInvokeResult toolInvoke:
                {
                    // / adds a tool to context and passes args to LLM
                    var tool = toolRegistry.GetSkill(toolInvoke.ToolName);
                    if (tool == null)
                    {
                        var availableTools = toolRegistry.GetAllSkills().Select(t => t.Name);
                        await WriteErrorEventAsync($"Tool '{toolInvoke.ToolName}' not found. Available: {string.Join(", ", availableTools.Take(20))}", ct);
                        return;
                    }

                    priorityTools = [tool.Name];

                    history.Add(new ChatMessage(ChatRole.System,
                        $"[Tool Context: {tool.Name}]\nThe user wants to use the '{tool.Name}' tool.\n" +
                        $"Description: {tool.Description}\n" +
                        $"Parameters: {System.Text.Json.JsonSerializer.Serialize(tool.Parameters)}\n" +
                        $"You MUST call the '{tool.Name}' tool to fulfill this request. " +
                        $"Do it in a SINGLE tool call — do not read files or do other steps first."));

                    var toolArgs = toolInvoke.RawArguments;

                    // Resolve @file references in args
                    await InjectFileReferencesAsync(toolArgs, history, ct);

                    request = request with { Message = string.IsNullOrWhiteSpace(toolArgs)
                        ? $"Use the {tool.Name} tool."
                        : toolArgs };
                    break;
                }

                case PlainMessageResult plain:
                {
                    await InjectFileReferencesAsync(plain.Message, history, ct);
                    break;
                }
            }

            // Resolve #toolInstance references from message + mounted agent prompt
            var combinedText = string.Join("\n", history.Where(h => h.Role == ChatRole.System).Select(h => h.Content ?? ""))
                + "\n" + request.Message;
            var instanceResolution = await toolInstanceResolver.ResolveAsync(combinedText, GetUserId(), ct);
            if (instanceResolution.InstanceArgs.Count > 0)
            {
                var instanceInfo = instanceResolution.InstanceArgs
                    .Select(kv => $"- Tool '{kv.Key}' has pre-filled args: {kv.Value}")
                    .ToList();
                history.Add(new ChatMessage(ChatRole.System,
                    $"[Tool Instances]\nThe following tools have pre-configured parameters from user settings. " +
                    $"When calling these tools, the pre-filled values will be automatically merged:\n{string.Join("\n", instanceInfo)}"));
            }

            // Convert image attachments to ImageContent
            var images = ConvertToImageContent(request.Images);

            // Stream LLM response with history (including any injected skill results)
            var streamUserId = GetUserId();
            var currentUser = currentUserProvider.GetCurrentUser();
            var sourceId = request.ConversationId?.ToString();
            var sourceName = conversation?.Title;

            await activityTracker.TrackAsync(streamUserId, currentUser.Name,
                ActivityType.Chat, ActivityStatus.Started, sourceId, sourceName, ct: ct);

            var eventStream = pipeline.ExecuteStreamAsync(request.Message, history, request.Language, images, streamUserId, currentWorkspaceProvider.WorkspaceId, currentUser.Roles, priorityTools, ct);

            await foreach (var evt in eventStream)
            {
                var data = JsonSerializer.Serialize(evt, JsonOptions);
                await Response.WriteAsync($"data: {data}\n\n", ct);
                await Response.Body.FlushAsync(ct);

                // Track activity based on stream events
                switch (evt.Type)
                {
                    case AgentStreamEventType.Thinking:
                        await activityTracker.TrackAsync(streamUserId, currentUser.Name,
                            ActivityType.Chat, ActivityStatus.Thinking, sourceId, sourceName, ct: ct);
                        break;
                    case AgentStreamEventType.ToolExecuting:
                        await activityTracker.TrackAsync(streamUserId, currentUser.Name,
                            ActivityType.ToolExecution, ActivityStatus.ToolExecuting, sourceId, sourceName, evt.ToolName, ct);
                        break;
                    case AgentStreamEventType.ToolCompleted:
                        await activityTracker.TrackAsync(streamUserId, currentUser.Name,
                            ActivityType.ToolExecution, ActivityStatus.Completed, sourceId, sourceName, evt.ToolName, ct);
                        break;
                }

                // Accumulate assistant response
                if (evt.Type == AgentStreamEventType.ContentDelta && evt.Content != null)
                {
                    assistantResponse += evt.Content;
                }
            }

            await activityTracker.TrackAsync(streamUserId, currentUser.Name,
                ActivityType.Chat, ActivityStatus.Completed, sourceId, sourceName, ct: ct);

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
            // Client disconnected — track as completed (not failed)
            var cancelUser = currentUserProvider.GetCurrentUser();
            await activityTracker.TrackAsync(GetUserId(), cancelUser.Name,
                ActivityType.Chat, ActivityStatus.Completed, request.ConversationId?.ToString(), detail: "Client disconnected");
        }
        catch (Exception ex)
        {
            var errorUser = currentUserProvider.GetCurrentUser();
            await activityTracker.TrackAsync(GetUserId(), errorUser.Name,
                ActivityType.Chat, ActivityStatus.Failed, request.ConversationId?.ToString(), detail: ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Extracts @file references from text and injects file contents into history.
    /// </summary>
    private async Task InjectFileReferencesAsync(string text, List<ChatMessage> history, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var fileRefs = System.Text.RegularExpressions.Regex.Matches(text, @"@([\w./\-]+\.\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct();

        var wsId = currentWorkspaceProvider.WorkspaceId;
        var isSuperAdmin = currentUserProvider.GetCurrentUser().Roles
            .Contains(Weda.Core.Application.Security.Models.Role.SuperAdmin);

        foreach (var fileRef in fileRefs)
        {
            try
            {
                var resolvedPath = PathSecurity.ResolveWorkspacePath(fileRef, wsId);
                var error = PathSecurity.ValidateWorkspacePath(resolvedPath, wsId, isSuperAdmin);
                if (error is not null) continue;
                if (!System.IO.File.Exists(resolvedPath)) continue;

                var content = await System.IO.File.ReadAllTextAsync(resolvedPath, ct);
                if (content.Length > 50_000) content = content[..50_000] + "\n... (truncated)";

                history.Add(new ChatMessage(ChatRole.System,
                    $"<file path=\"{fileRef}\">\n{content}\n</file>"));
            }
            catch { /* skip unreadable files */ }
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

        var userId = GetUserId();
        var conversation = await repository.GetByIdAndUserAsync(conversationId.Value, userId, ct);
        if (conversation == null)
            return (null, [], false);

        var history = conversation.Messages.Select(m => m.ToLlmMessage()).ToList();
        var isFirstMessage = conversation.Messages.Count == 0;

        // Compact history using refreshing agent compressor
        var llmProvider = await llmProviderFactory.GetProviderAsync(GetUserId(), ct: ct);
        history = await contextCompressor.CompressIfNeededAsync(history, llmProvider, ct: ct);

        return (conversation, history, isFirstMessage);
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
            var llmProvider = await llmProviderFactory.GetProviderAsync(GetUserId(), ct: ct);
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

}