using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Pipelines;

public class AdoTaskSyncPipeline(
    IServiceProvider serviceProvider,
    ILogger<AdoTaskSyncPipeline> logger) : IToolPipeline
{
    public string Name => "ado_task_sync";
    public string Description => "Sync Azure DevOps tasks based on git history and code analysis";

    public object? Parameters => new
    {
        type = "object",
        properties = new
        {
            repoFilter = new
            {
                type = "string",
                description = "Optional: Filter to only analyze specific repo (by name)"
            },
            commitCount = new
            {
                type = "integer",
                description = "Number of recent commits to analyze (default: 20)",
                @default = 20
            },
            diffRange = new
            {
                type = "integer",
                description = "Number of commits to include in diff analysis (default: 10)",
                @default = 10
            },
            testApproval = new
            {
                type = "boolean",
                description = "Test mode: force approval dialog with sample data (for UI testing)",
                @default = false
            }
        }
    };

    public async Task<ToolPipelineResult> RunAsync(
        PipelineExecutionContext context,
        Func<PipelineApprovalRequest, Task<bool>>? onApprovalRequired = null,
        CancellationToken ct = default)
    {
        // Parse args
        var args = ParseArgs(context.ArgsJson);
        var steps = new List<ToolStepResult>();

        // Test mode: skip all processing and directly trigger approval dialog
        if (args.TestApproval)
        {
            logger.LogInformation("Test approval mode enabled - triggering sample approval dialog");
            steps.Add(new ToolStepResult("test_mode", true, Output: "Test approval mode"));

            var testChanges = new List<ProposedChange>
            {
                new ProposedChange(
                    WorkItemId: 12345,
                    Title: "Sample Task: Implement feature X",
                    WorkItemType: "Task",
                    CurrentState: "To Do",
                    ProposedState: "Doing",
                    Reason: "Found related commits implementing this feature",
                    RelatedCommits: new[] { "abc1234: feat: add feature X implementation", "def5678: fix: resolve edge case in feature X" },
                    WorkItemUrl: "https://dev.azure.com/org/project/_workitems/edit/12345"),
                new ProposedChange(
                    WorkItemId: 12346,
                    Title: "Sample Bug: Fix login issue",
                    WorkItemType: "Bug",
                    CurrentState: "Active",
                    ProposedState: "Resolved",
                    Reason: "Bug fix committed and verified",
                    RelatedCommits: new[] { "ghi9012: fix: resolve login timeout issue" },
                    WorkItemUrl: "https://dev.azure.com/org/project/_workitems/edit/12346")
            };

            var approvalRequest = new PipelineApprovalRequest(
                "test_batch_update",
                $"[TEST] Update {testChanges.Count} work item(s) - this is a test of the approval UI",
                testChanges);

            var approved = onApprovalRequired != null
                ? await onApprovalRequired(approvalRequest)
                : false;

            steps.Add(new ToolStepResult("test_approval", approved, Output: approved ? "Approved" : "Rejected"));
            return new ToolPipelineResult(true, $"Test approval completed: {(approved ? "Approved" : "Rejected")}", steps);
        }

        var skills = serviceProvider.GetServices<IAgentTool>().ToList();

        var gitSkill = skills.FirstOrDefault(s => s.Name == "git");
        var adoSkill = skills.FirstOrDefault(s => s.Name == "azure_devops");

        if (gitSkill == null || adoSkill == null)
        {
            return new ToolPipelineResult(
                false,
                "Required skills not found",
                [new ToolStepResult("init", false, Error: $"Git: {gitSkill != null}, ADO: {adoSkill != null}")]);
        }

        // Step 1: Get tracked repos from user preferences
        logger.LogInformation("Step 1: Getting tracked repos from user preferences (userId: {UserId})...", context.UserId);

        List<TrackedProject> trackedProjects;
        string? adoOrganization = null;
        string? adoProject = null;
        string? adoQueryId = null;

        if (context.UserId.HasValue)
        {
            // Create a new scope for database access in background task
            string? trackedProjectsValue;
            using (var scope = serviceProvider.CreateScope())
            {
                var preferenceRepo = scope.ServiceProvider.GetRequiredService<IUserPreferenceRepository>();
                var pref = await preferenceRepo.GetByKeyAsync(context.UserId.Value, "ado_tracked_projects", ct);
                trackedProjectsValue = pref?.Value;

                // Also read ADO preferences for skill calls
                var orgPref = await preferenceRepo.GetByKeyAsync(context.UserId.Value, "ado_organization", ct);
                var projPref = await preferenceRepo.GetByKeyAsync(context.UserId.Value, "ado_project", ct);
                var queryPref = await preferenceRepo.GetByKeyAsync(context.UserId.Value, "ado_query_id", ct);
                adoOrganization = orgPref?.Value;
                adoProject = projPref?.Value;
                adoQueryId = queryPref?.Value;
            }

            if (string.IsNullOrWhiteSpace(trackedProjectsValue))
            {
                var error = "ado_tracked_projects preference is not set. Set it to a JSON array of local repo paths in User Preferences.";
                steps.Add(new ToolStepResult("get_tracked_projects", false, Error: error));
                return new ToolPipelineResult(false, error, steps);
            }

            logger.LogInformation("Found ado_tracked_projects preference: {Value}", trackedProjectsValue);
            logger.LogInformation("ADO preferences - org: {Org}, project: {Project}", adoOrganization, adoProject);

            // Get tracked repos using skill (which reads from local git repos)
            var trackedResult = await ExecuteSkillAsync(adoSkill, new { operation = "list_tracked_repos", trackedProjects = trackedProjectsValue }, ct);
            logger.LogInformation("Step 1 result: IsSuccess={IsSuccess}, Output={Output}, Error={Error}",
                trackedResult.IsSuccess,
                trackedResult.Output?.Substring(0, Math.Min(200, trackedResult.Output?.Length ?? 0)),
                trackedResult.Error);
            steps.Add(new ToolStepResult("list_tracked_repos", trackedResult.IsSuccess, trackedResult.Output, trackedResult.Error));

            if (!trackedResult.IsSuccess)
            {
                logger.LogError("Step 1 failed: {Error}", trackedResult.Error);
                return new ToolPipelineResult(false, $"Failed to get tracked repos: {trackedResult.Error}", steps);
            }

            trackedProjects = ParseTrackedProjects(trackedResult.Output);
        }
        else
        {
            var error = "No user context available. Pipeline must be executed with a valid user.";
            steps.Add(new ToolStepResult("get_tracked_projects", false, Error: error));
            return new ToolPipelineResult(false, error, steps);
        }

        if (trackedProjects.Count == 0)
        {
            return new ToolPipelineResult(false, "No tracked repos configured", steps);
        }

        // Apply repo filter if specified
        if (!string.IsNullOrEmpty(args.RepoFilter))
        {
            trackedProjects = trackedProjects
                .Where(p => p.RepoName.Contains(args.RepoFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (trackedProjects.Count == 0)
            {
                return new ToolPipelineResult(false, $"No repos match filter: {args.RepoFilter}", steps);
            }

            logger.LogInformation("Filtered to {Count} repos matching '{Filter}'", trackedProjects.Count, args.RepoFilter);
        }

        // Step 2: For each tracked repo, get git log and work items
        var proposedChanges = new List<ProposedChange>();

        foreach (var project in trackedProjects)
        {
            logger.LogInformation("Processing repo: {Repo}", project.RepoName);

            // Step 2a: Get git log for recent commits (use args.CommitCount)
            var gitLogResult = await ExecuteSkillAsync(gitSkill, new { command = $"log --oneline -{args.CommitCount}", workingDirectory = project.LocalPath }, ct);
            steps.Add(new ToolStepResult($"git_log:{project.RepoName}", gitLogResult.IsSuccess, gitLogResult.Output, gitLogResult.Error));

            // Step 2b: Get work items for this repo (pass ADO preferences explicitly)
            var workItemsResult = await ExecuteSkillAsync(adoSkill, new
            {
                operation = "get_work_items_by_repo",
                repoName = project.RepoName,
                organization = adoOrganization,
                project = adoProject,
                queryId = adoQueryId
            }, ct);
            steps.Add(new ToolStepResult($"get_work_items:{project.RepoName}", workItemsResult.IsSuccess, workItemsResult.Output, workItemsResult.Error));

            if (!workItemsResult.IsSuccess)
                continue;

            // Step 2c: Get git diff for detailed analysis (use args.DiffRange)
            var gitDiffResult = await ExecuteSkillAsync(gitSkill, new { command = $"diff HEAD~{args.DiffRange} --stat", workingDirectory = project.LocalPath }, ct);
            steps.Add(new ToolStepResult($"git_diff:{project.RepoName}", gitDiffResult.IsSuccess, gitDiffResult.Output, gitDiffResult.Error));

            // Step 2d: Analyze with LLM and prepare changes
            var changes = await AnalyzeWithLlmAsync(
                workItemsResult.Output,
                gitLogResult.Output,
                gitDiffResult.Output,
                project.RepoName,
                ct);
            proposedChanges.AddRange(changes);
        }

        // Step 3: Request user approval before updating
        if (proposedChanges.Count > 0)
        {
            logger.LogInformation("Found {Count} proposed changes, requesting approval...", proposedChanges.Count);

            var approvalRequest = new PipelineApprovalRequest(
                "batch_update",
                $"Update {proposedChanges.Count} work item(s) based on git commit analysis",
                proposedChanges);

            var approved = onApprovalRequired != null
                ? await onApprovalRequired(approvalRequest)
                : false;

            if (!approved)
            {
                steps.Add(new ToolStepResult("batch_update", false, Error: "User rejected the changes"));
                return new ToolPipelineResult(false, "Changes rejected by user", steps);
            }

            // Step 4: Execute approved updates
            logger.LogInformation("Approval granted, updating {Count} work items...", proposedChanges.Count);

            var updates = proposedChanges.Select(c => new
            {
                id = c.WorkItemId,
                fields = new Dictionary<string, object>
                {
                    ["System.State"] = c.ProposedState,
                    ["System.History"] = c.Reason
                }
            }).ToList();

            var updatesJson = JsonSerializer.Serialize(updates);

            var batchResult = await ExecuteSkillAsync(adoSkill, new
            {
                operation = "batch_update",
                updates = updatesJson,
                organization = adoOrganization,
                project = adoProject
            }, ct);
            steps.Add(new ToolStepResult("batch_update", batchResult.IsSuccess, batchResult.Output, batchResult.Error));

            if (!batchResult.IsSuccess)
            {
                return new ToolPipelineResult(false, $"Batch update failed: {batchResult.Error}", steps);
            }
        }
        else
        {
            steps.Add(new ToolStepResult("batch_update", true, Output: "No updates needed"));
        }

        var summary = $"Processed {trackedProjects.Count} repos, updated {proposedChanges.Count} work items";
        logger.LogInformation("Pipeline complete: {Summary}", summary);

        return new ToolPipelineResult(true, summary, steps);
    }

    private async Task<ToolResult> ExecuteSkillAsync(IAgentTool skill, object args, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(args);
        logger.LogDebug("Executing skill {SkillName} with args: {Args}", skill.Name, json);
        var context = new ToolContext(json);
        var result = await skill.ExecuteAsync(context, ct);
        logger.LogDebug("Skill {SkillName} result: IsSuccess={IsSuccess}", skill.Name, result.IsSuccess);
        return result;
    }

    private static List<TrackedProject> ParseTrackedProjects(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        try
        {
            var json = JsonDocument.Parse(output);
            var projects = new List<TrackedProject>();

            if (json.RootElement.TryGetProperty("trackedProjects", out var trackedArray))
            {
                foreach (var item in trackedArray.EnumerateArray())
                {
                    var localPath = item.GetProperty("localPath").GetString();
                    var repoName = item.GetProperty("repoName").GetString();

                    if (!string.IsNullOrEmpty(localPath) && !string.IsNullOrEmpty(repoName))
                    {
                        projects.Add(new TrackedProject(localPath, repoName));
                    }
                }
            }

            return projects;
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<ProposedChange>> AnalyzeWithLlmAsync(
        string? workItemsJson,
        string? gitLog,
        string? gitDiff,
        string repoName,
        CancellationToken ct)
    {
        var changes = new List<ProposedChange>();

        if (string.IsNullOrWhiteSpace(workItemsJson))
            return changes;

        try
        {
            var json = JsonDocument.Parse(workItemsJson);

            // ADO API returns { count, value: [...] }
            if (!json.RootElement.TryGetProperty("value", out var workItems))
                return changes;

            // Create new scope to avoid ObjectDisposedException in background task
            using var scope = serviceProvider.CreateScope();
            var scopedLlmFactory = scope.ServiceProvider.GetRequiredService<ILlmProviderFactory>();
            var llmProvider = await scopedLlmFactory.GetProviderAsync(ct);

            foreach (var workItem in workItems.EnumerateArray())
            {
                var id = workItem.GetProperty("id").GetInt32();

                // Extract work item details
                string? state = null;
                string? title = null;
                string? workItemType = null;

                if (workItem.TryGetProperty("fields", out var fields))
                {
                    if (fields.TryGetProperty("System.State", out var stateElem))
                        state = stateElem.GetString();
                    if (fields.TryGetProperty("System.Title", out var titleElem))
                        title = titleElem.GetString();
                    if (fields.TryGetProperty("System.WorkItemType", out var typeElem))
                        workItemType = typeElem.GetString();
                }

                // Skip if already closed or done
                if (state is "Closed" or "Done" or "Resolved" or "Removed")
                {
                    logger.LogDebug("Skipping work item {Id} ({Title}) - already {State}", id, title, state);
                    continue;
                }

                logger.LogInformation("Analyzing work item {Id}: {Title} (State: {State})", id, title, state);

                // Use LLM to analyze if this work item has related commits
                var analysis = await AnalyzeWorkItemWithLlmAsync(
                    llmProvider,
                    id,
                    title ?? "Unknown",
                    workItemType ?? "Task",
                    state ?? "Unknown",
                    gitLog,
                    gitDiff,
                    repoName,
                    ct);

                if (analysis.HasProgress && analysis.ProposedState != state)
                {
                    logger.LogInformation(
                        "LLM analysis for work item {Id}: {CurrentState} -> {ProposedState}. Reason: {Reason}",
                        id, state, analysis.ProposedState, analysis.Reason);

                    // Build ADO work item URL
                    var workItemUrl = workItem.TryGetProperty("url", out var urlElem)
                        ? urlElem.GetString()?.Replace("_apis/wit/workItems", "_workitems/edit")
                        : null;

                    changes.Add(new ProposedChange(
                        WorkItemId: id,
                        Title: title ?? "Unknown",
                        WorkItemType: workItemType ?? "Task",
                        CurrentState: state ?? "Unknown",
                        ProposedState: analysis.ProposedState,
                        Reason: analysis.Reason,
                        RelatedCommits: analysis.RelatedCommits,
                        WorkItemUrl: workItemUrl));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to analyze work items for {Repo}", repoName);
        }

        return changes;
    }

    private async Task<WorkItemAnalysis> AnalyzeWorkItemWithLlmAsync(
        ILlmProvider llmProvider,
        int workItemId,
        string title,
        string workItemType,
        string currentState,
        string? gitLog,
        string? gitDiff,
        string repoName,
        CancellationToken ct)
    {
        var prompt = $$"""
            You are analyzing Azure DevOps work items against git history to determine if any progress has been made.

            ## Work Item
            - ID: {{workItemId}}
            - Type: {{workItemType}}
            - Title: {{title}}
            - Current State: {{currentState}}
            - Repository: {{repoName}}

            ## Recent Git Commits
            ```
            {{gitLog ?? "(no commits)"}}
            ```

            ## Recent Code Changes (diff --stat)
            ```
            {{gitDiff ?? "(no changes)"}}
            ```

            ## Task
            Analyze if any of the commits or code changes are related to this work item.
            Consider:
            1. Does any commit message mention this work item ID (#{{workItemId}}, AB#{{workItemId}})?
            2. Does any commit message semantically relate to the work item title?
            3. Do the code changes appear to implement functionality described in the title?

            ## Response Format
            Respond in JSON format only:
            ```json
            {
              "hasProgress": true/false,
              "proposedState": "To Do" | "Doing" | "Done",
              "confidence": "high" | "medium" | "low",
              "reason": "Brief explanation of why you think there is/isn't progress",
              "relatedCommits": ["commit hash: message", ...]
            }
            ```

            Only propose "Done" if the work appears fully complete. Propose "Doing" if work has started but may not be complete.
            In "relatedCommits", list the commit hashes and messages that are related to this work item (empty array if none).
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        try
        {
            var response = await llmProvider.ChatAsync(messages, ct: ct);
            var content = response.Content ?? "";

            logger.LogDebug("LLM response for work item {Id}: {Content}", workItemId, content);

            // Extract JSON from response
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = content[jsonStart..(jsonEnd + 1)];
                var result = JsonSerializer.Deserialize<LlmAnalysisResult>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null)
                {
                    logger.LogDebug(
                        "Work item {Id} analysis result: HasProgress={HasProgress}, ProposedState={ProposedState}, Confidence={Confidence}, RelatedCommits={CommitCount}",
                        workItemId, result.HasProgress, result.ProposedState, result.Confidence, result.RelatedCommits?.Count ?? 0);

                    return new WorkItemAnalysis(
                        result.HasProgress,
                        result.ProposedState ?? currentState,
                        result.Reason ?? "LLM analysis",
                        result.RelatedCommits);
                }
            }

            logger.LogWarning("Failed to parse LLM response for work item {Id}", workItemId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM analysis failed for work item {Id}", workItemId);
        }

        return new WorkItemAnalysis(false, currentState, "Analysis failed");
    }

    private record WorkItemAnalysis(
        bool HasProgress,
        string ProposedState,
        string Reason,
        IReadOnlyList<string>? RelatedCommits = null);

    private record LlmAnalysisResult(
        bool HasProgress,
        string? ProposedState,
        string? Confidence,
        string? Reason,
        IReadOnlyList<string>? RelatedCommits = null);

    private record TrackedProject(string LocalPath, string RepoName);

    private record PipelineArgs(string? RepoFilter, int CommitCount, int DiffRange, bool TestApproval);

    private static PipelineArgs ParseArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
            return new PipelineArgs(null, 20, 10, false);

        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            string? repoFilter = null;
            int commitCount = 20;
            int diffRange = 10;
            bool testApproval = false;

            if (root.TryGetProperty("repoFilter", out var rf) && rf.ValueKind == JsonValueKind.String)
                repoFilter = rf.GetString();
            if (root.TryGetProperty("commitCount", out var cc) && cc.ValueKind == JsonValueKind.Number)
                commitCount = cc.GetInt32();
            if (root.TryGetProperty("diffRange", out var dr) && dr.ValueKind == JsonValueKind.Number)
                diffRange = dr.GetInt32();
            if (root.TryGetProperty("testApproval", out var ta) && ta.ValueKind == JsonValueKind.True)
                testApproval = true;

            return new PipelineArgs(repoFilter, commitCount, diffRange, testApproval);
        }
        catch
        {
            return new PipelineArgs(null, 20, 10, false);
        }
    }
}
