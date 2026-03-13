using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Users.Queries;

namespace OpenClaw.Skills.AzureDevOps.AzureDevOpsCommands;

public class AzureDevOpsSkill(
    IServiceProvider serviceProvider,
    ILogger<AzureDevOpsSkill> logger) : AgentSkillBase<AzureDevOpsSkillArgs>
{
    private const string BaseUrl = "https://dev.azure.com";

    // Execution context - set per request
    private HttpClient _client = null!;
    private string _org = null!;
    private string _project = null!;
    private string? _queryId;
    private string? _assignedTo;
    private ISender _mediator = null!;
    private GitRepoMapper _gitRepoMapper = null!;
    private CancellationToken _ct;

    public override string Name => "azure_devops";
    public override string Description => """
        Azure DevOps operations. All parameters have defaults from user preferences (ado_organization, ado_project, ado_query_id, ado_assigned_to).
        When user asks for "my tasks" or "run query", just call the operation WITHOUT asking for parameters - defaults will be used automatically.
        - run_query: Execute user's saved query (uses ado_query_id preference)
        - my_work_items: Get current sprint items
        - list_tracked_repos: List locally tracked repos with their ADO remote info (uses ado_tracked_projects preference)
        """;

    public override async Task<SkillResult> ExecuteAsync(AzureDevOpsSkillArgs args, CancellationToken ct)
    {
        _ct = ct;
        using var scope = serviceProvider.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();
        _mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        _gitRepoMapper = scope.ServiceProvider.GetRequiredService<GitRepoMapper>();

        // Try to get config values with error handling
        string? patFromConfig = null;
        string? orgFromConfig = null;
        try
        {
            patFromConfig = configStore.Get(ConfigKeys.AzureDevOpsPat);
            orgFromConfig = configStore.Get(ConfigKeys.AzureDevOpsOrg);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error reading from config store");
        }

        // Get user preferences for defaults
        logger.LogDebug("Fetching user preferences...");
        var orgFromPref = await GetPreferenceAsync("ado_organization");
        var projectFromPref = await GetPreferenceAsync("ado_project");
        var queryIdFromPref = await GetPreferenceAsync("ado_query_id");
        var assignedToFromPref = await GetPreferenceAsync("ado_assigned_to");
        logger.LogDebug("Preferences loaded - org: {Org}, project: {Project}, queryId: {QueryId}",
            orgFromPref ?? "null", projectFromPref ?? "null", queryIdFromPref ?? "null");

        var patFromEnv = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
        var pat = patFromConfig ?? patFromEnv;

        var orgFromEnv = Environment.GetEnvironmentVariable("AZURE_DEVOPS_ORG");
        // Priority: args > config > preference > env
        _org = (args.Organization ?? orgFromConfig ?? orgFromPref ?? orgFromEnv)!;
        _project = (args.Project ?? projectFromPref)!;
        _queryId = args.QueryId ?? queryIdFromPref;
        _assignedTo = args.AssignedTo ?? assignedToFromPref;

        // Debug logging
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var patSource = patFromConfig != null ? "config" : patFromEnv != null ? "env" : "none";
            var orgSource = args.Organization != null ? "args" : orgFromConfig != null ? "config" : orgFromPref != null ? "pref" : "env";
            var projectSource = args.Project != null ? "args" : projectFromPref != null ? "pref" : "none";

            logger.LogDebug("PAT source: {PatSource}, length: {PatLength}", patSource, pat?.Length ?? 0);
            logger.LogDebug("Organization: {Org} (from: {OrgSource})", _org ?? "null", orgSource);
            logger.LogDebug("Project: {Project} (from: {ProjectSource})", _project ?? "null", projectSource);
            logger.LogDebug("QueryId: {QueryId}", _queryId ?? "null");
        }

        if (string.IsNullOrWhiteSpace(pat))
            return SkillResult.Failure("AZURE_DEVOPS_PAT is not set. Configure it in Settings > Configs with key 'AZURE_DEVOPS_PAT'.");

        if (string.IsNullOrWhiteSpace(_org))
            return SkillResult.Failure("Organization is required. Set AZURE_DEVOPS_ORG, configure ado_organization preference, or provide 'organization' parameter.");

        if (string.IsNullOrWhiteSpace(_project))
            return SkillResult.Failure("Project is required. Set ado_project preference or provide 'project' parameter.");

        _client = CreateClient(pat);

        if (string.IsNullOrWhiteSpace(args.Operation))
            return SkillResult.Failure("Operation is required. Valid operations: run_query, my_work_items, list_tracked_repos, get_work_items_by_repo, update_work_item, list_repos, list_iterations, get_work_item, list_builds, list_pipelines, list_prs");

        try
        {
            return args.Operation.ToLowerInvariant() switch
            {
                "my_work_items" => await GetMyWorkItemsAsync(),
                "list_repos" => await ListReposAsync(),
                "list_iterations" => await ListIterationsAsync(args.Team),
                "get_work_item" => await GetWorkItemAsync(args.WorkItemId),
                "list_builds" => await ListBuildsAsync(args.Top),
                "list_pipelines" => await ListPipelinesAsync(),
                "list_prs" => await ListPullRequestsAsync(args.Repository),
                "run_query" => await RunSavedQueryAsync(),
                "list_tracked_repos" => await ListTrackedReposAsync(),
                "update_work_item" => await UpdateWorkItemAsync(args.WorkItemId, args.Fields),
                "get_work_items_by_repo" => await GetWorkItemsByRepoAsync(args.RepoName),
                _ => SkillResult.Failure($"Unknown operation: {args.Operation}. Valid: my_work_items, list_repos, list_iterations, get_work_item, list_builds, list_pipelines, list_prs, run_query, list_tracked_repos, update_work_item, get_work_items_by_repo")
            };
        }
        catch (HttpRequestException ex)
        {
            return SkillResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Error: {ex.Message}");
        }
        finally
        {
            _client.Dispose();
        }
    }

    private static HttpClient CreateClient(string pat)
    {
        var client = new HttpClient();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private async Task<SkillResult> GetMyWorkItemsAsync()
    {
        var assignee = string.IsNullOrWhiteSpace(_assignedTo) ? "@Me" : $"'{_assignedTo}'";

        var wiql = $"""
            SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], [System.AssignedTo], [Microsoft.VSTS.Scheduling.RemainingWork]
            FROM WorkItems
            WHERE [System.TeamProject] = @project
              AND [System.AssignedTo] = {assignee}
              AND [System.IterationPath] = @currentIteration
              AND [System.State] <> 'Closed'
              AND [System.State] <> 'Removed'
            ORDER BY [System.WorkItemType], [System.State]
            """;

        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/wiql?api-version=7.1";
        var content = new StringContent(JsonSerializer.Serialize(new { query = wiql }), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        var wiqlResult = JsonSerializer.Deserialize<WiqlResponse>(data, JsonOptions);
        if (wiqlResult?.WorkItems == null || wiqlResult.WorkItems.Count == 0)
            return SkillResult.Success("No work items found for current iteration.");

        var ids = wiqlResult.WorkItems.Take(50).Select(w => w.Id);
        var detailsUrl = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/workitems?ids={string.Join(",", ids)}&fields=System.Id,System.Title,System.State,System.WorkItemType,System.AssignedTo,Microsoft.VSTS.Scheduling.RemainingWork,System.Tags&api-version=7.1";

        var detailsResponse = await _client.GetAsync(detailsUrl, _ct);
        var detailsData = await detailsResponse.Content.ReadAsStringAsync(_ct);

        if (!detailsResponse.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({detailsResponse.StatusCode}): {detailsData}");

        return SkillResult.Success(FormatJson(detailsData));
    }

    private async Task<SkillResult> ListReposAsync()
    {
        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/git/repositories?api-version=7.1";

        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private async Task<SkillResult> ListIterationsAsync(string? team)
    {
        var teamName = team ?? $"{_project} Team";
        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/{Uri.EscapeDataString(teamName)}/_apis/work/teamsettings/iterations?api-version=7.1";

        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private async Task<SkillResult> GetWorkItemAsync(int? workItemId)
    {
        if (workItemId == null)
            return SkillResult.Failure("workItemId is required.");

        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/workitems/{workItemId}?$expand=all&api-version=7.1";

        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private async Task<SkillResult> ListBuildsAsync(int? top)
    {
        var topCount = top ?? 10;
        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/build/builds?$top={topCount}&api-version=7.1";

        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private async Task<SkillResult> ListPipelinesAsync()
    {
        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/pipelines?api-version=7.1";

        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private async Task<SkillResult> ListPullRequestsAsync(string? repository)
    {
        if (string.IsNullOrWhiteSpace(repository))
            return SkillResult.Failure("repository is required for list_prs operation.");

        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/git/repositories/{Uri.EscapeDataString(repository)}/pullrequests?searchCriteria.status=active&api-version=7.1";

        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private async Task<SkillResult> RunSavedQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(_queryId))
            return SkillResult.Failure("queryId is required. Set ado_query_id preference or provide 'queryId' parameter.");

        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/wiql/{_queryId}?api-version=7.1";

        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        var wiqlResult = JsonSerializer.Deserialize<WiqlResponse>(data, JsonOptions);
        if (wiqlResult?.WorkItems == null || wiqlResult.WorkItems.Count == 0)
            return SkillResult.Success("No work items found in query results.");

        // Fetch detailed work item information (up to 200 items)
        var ids = wiqlResult.WorkItems.Take(200).Select(w => w.Id);
        var detailsUrl = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/workitems?ids={string.Join(",", ids)}&fields=System.Id,System.Title,System.State,System.WorkItemType,System.AssignedTo,Microsoft.VSTS.Scheduling.RemainingWork,System.IterationPath,System.AreaPath,System.Tags&api-version=7.1";

        var detailsResponse = await _client.GetAsync(detailsUrl, _ct);
        var detailsData = await detailsResponse.Content.ReadAsStringAsync(_ct);

        if (!detailsResponse.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({detailsResponse.StatusCode}): {detailsData}");

        return SkillResult.Success(FormatJson(detailsData));
    }

    private async Task<SkillResult> GetWorkItemsByRepoAsync(string? repoName)
    {
        if (string.IsNullOrWhiteSpace(_queryId))
            return SkillResult.Failure("queryId is required. Set ado_query_id preference.");

        if (string.IsNullOrWhiteSpace(repoName))
            return SkillResult.Failure("repoName is required for get_work_items_by_repo.");

        // First, run the query to get all work items
        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/wiql/{_queryId}?api-version=7.1";
        var response = await _client.GetAsync(url, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        var wiqlResult = JsonSerializer.Deserialize<WiqlResponse>(data, JsonOptions);
        if (wiqlResult?.WorkItems == null || wiqlResult.WorkItems.Count == 0)
            return SkillResult.Success("No work items found in query results.");

        // Fetch detailed work item information including Tags
        var ids = wiqlResult.WorkItems.Take(200).Select(w => w.Id);
        var detailsUrl = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/workitems?ids={string.Join(",", ids)}&fields=System.Id,System.Title,System.State,System.WorkItemType,System.AssignedTo,Microsoft.VSTS.Scheduling.RemainingWork,System.IterationPath,System.AreaPath,System.Tags&api-version=7.1";

        var detailsResponse = await _client.GetAsync(detailsUrl, _ct);
        var detailsData = await detailsResponse.Content.ReadAsStringAsync(_ct);

        if (!detailsResponse.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({detailsResponse.StatusCode}): {detailsData}");

        // Parse and filter by repo tag
        var repoTag = $"repo:{repoName}";
        using var doc = JsonDocument.Parse(detailsData);

        var filteredItems = new List<JsonElement>();
        if (doc.RootElement.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("fields", out var fields) &&
                    fields.TryGetProperty("System.Tags", out var tags))
                {
                    var tagsStr = tags.GetString() ?? "";
                    if (tagsStr.Split(';').Select(t => t.Trim()).Contains(repoTag, StringComparer.OrdinalIgnoreCase))
                    {
                        filteredItems.Add(item.Clone());
                    }
                }
            }
        }

        var result = new { count = filteredItems.Count, value = filteredItems };
        return SkillResult.Success(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<SkillResult> UpdateWorkItemAsync(int? workItemId, string? fieldsJson)
    {
        if (workItemId == null)
            return SkillResult.Failure("workItemId is required for update_work_item.");

        if (string.IsNullOrWhiteSpace(fieldsJson))
            return SkillResult.Failure("fields is required. Provide a JSON object like: {\"System.State\": \"Active\", \"Custom.GitRepository\": \"repo-name\"}");

        Dictionary<string, object>? fields;
        try
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, object>>(fieldsJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return SkillResult.Failure($"Invalid fields JSON: {ex.Message}");
        }

        if (fields == null || fields.Count == 0)
            return SkillResult.Failure("No fields to update.");

        // Build JSON Patch document
        var patchOperations = fields.Select(f => new
        {
            op = "add",
            path = $"/fields/{f.Key}",
            value = f.Value
        }).ToList();

        var patchJson = JsonSerializer.Serialize(patchOperations);
        var content = new StringContent(patchJson, Encoding.UTF8, "application/json-patch+json");

        var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(_project)}/_apis/wit/workitems/{workItemId}?api-version=7.1";

        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await _client.SendAsync(request, _ct);
        var data = await response.Content.ReadAsStringAsync(_ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private async Task<SkillResult> ListTrackedReposAsync()
    {
        var trackedProjectsJson = await GetPreferenceAsync("ado_tracked_projects");
        if (string.IsNullOrWhiteSpace(trackedProjectsJson))
        {
            return SkillResult.Failure("ado_tracked_projects preference is not set. Set it to a JSON array of local repo paths.");
        }

        List<string>? paths;
        try
        {
            paths = JsonSerializer.Deserialize<List<string>>(trackedProjectsJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return SkillResult.Failure($"Invalid ado_tracked_projects format. Expected JSON array. Error: {ex.Message}");
        }

        if (paths == null || paths.Count == 0)
        {
            return SkillResult.Success("No tracked projects configured.");
        }

        var localRepos = await _gitRepoMapper.GetTrackedReposAsync(paths, _ct);

        // Group by project to minimize API calls
        var reposByProject = localRepos
            .Where(r => r.IsAdoRepo && r.Organization?.Equals(_org, StringComparison.OrdinalIgnoreCase) == true)
            .GroupBy(r => r.Project!);

        var results = new List<object>();

        foreach (var projectGroup in reposByProject)
        {
            var project = projectGroup.Key;
            var url = $"{BaseUrl}/{_org}/{Uri.EscapeDataString(project)}/_apis/git/repositories?api-version=7.1";

            var response = await _client.GetAsync(url, _ct);
            var data = await response.Content.ReadAsStringAsync(_ct);

            Dictionary<string, JsonElement>? adoRepos = null;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("value", out var value))
                    {
                        adoRepos = value.EnumerateArray()
                            .Where(r => r.TryGetProperty("name", out _))
                            .ToDictionary(
                                r => r.GetProperty("name").GetString()!,
                                r => r.Clone());
                    }
                }
                catch { /* ignore parse errors */ }
            }

            foreach (var localRepo in projectGroup)
            {
                var adoInfo = adoRepos?.GetValueOrDefault(localRepo.Repository!);
                results.Add(new
                {
                    localRepo.LocalPath,
                    localRepo.RemoteUrl,
                    localRepo.Organization,
                    localRepo.Project,
                    localRepo.Repository,
                    localRepo.IsAdoRepo,
                    localRepo.AdoRepoPath,
                    AdoRepoId = adoInfo?.GetProperty("id").GetString(),
                    DefaultBranch = adoInfo?.GetProperty("defaultBranch").GetString()?.Replace("refs/heads/", ""),
                    WebUrl = adoInfo?.GetProperty("webUrl").GetString()
                });
            }
        }

        // Add repos not matching current org
        foreach (var repo in localRepos.Where(r => !r.IsAdoRepo || r.Organization?.Equals(_org, StringComparison.OrdinalIgnoreCase) != true))
        {
            results.Add(new
            {
                repo.LocalPath,
                repo.RemoteUrl,
                repo.Organization,
                repo.Project,
                repo.Repository,
                repo.IsAdoRepo,
                repo.AdoRepoPath,
                AdoRepoId = (string?)null,
                DefaultBranch = (string?)null,
                WebUrl = (string?)null
            });
        }

        return SkillResult.Success(JsonSerializer.Serialize(new { TrackedRepos = results }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return json; }
    }

    private async Task<string?> GetPreferenceAsync(string key)
    {
        try
        {
            logger.LogTrace("Getting preference: {Key}", key);
            var result = await _mediator.Send(new GetUserPreferenceQuery(key), _ct);
            var value = result.Match(
                pref =>
                {
                    logger.LogTrace("Preference '{Key}' = '{Value}'", key, pref?.Value ?? "null");
                    return pref?.Value;
                },
                errors =>
                {
                    logger.LogDebug("Preference '{Key}' error: {Error}", key, errors.First().Description);
                    return null;
                });
            return value;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preference '{Key}' exception", key);
            return null;
        }
    }
}

internal class WiqlResponse
{
    [JsonPropertyName("workItems")]
    public List<WorkItemRef>? WorkItems { get; set; }
}

internal class WorkItemRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public record AzureDevOpsSkillArgs(
    [property: Description("""
        The operation to perform:
        - run_query: Execute user's saved query (no parameters needed, uses ado_query_id preference)
        - my_work_items: Get my work items in current iteration (no parameters needed)
        - list_tracked_repos: List locally tracked repos with their ADO remote info (no parameters needed)
        - get_work_items_by_repo: Get work items tagged with repo:xxx (requires repoName)
        - update_work_item: Update work item fields (requires workItemId and fields)
        - list_repos: List all repositories
        - list_iterations: List iterations/sprints
        - get_work_item: Get work item details (requires workItemId)
        - list_builds: List recent builds
        - list_pipelines: List all pipelines
        - list_prs: List pull requests (requires repository)
        """)]
    string Operation,

    [property: Description("Azure DevOps project name. Defaults to ado_project preference")]
    string? Project = null,

    [property: Description("Azure DevOps organization name. Defaults to ado_organization preference or AZURE_DEVOPS_ORG env var")]
    string? Organization = null,

    [property: Description("User display name for my_work_items. Defaults to ado_assigned_to preference or @Me")]
    string? AssignedTo = null,

    [property: Description("Team name for list_iterations. Defaults to '{Project} Team'")]
    string? Team = null,

    [property: Description("Work item ID for get_work_item")]
    int? WorkItemId = null,

    [property: Description("Saved query ID for run_query. Usually NOT needed - uses ado_query_id preference automatically")]
    string? QueryId = null,

    [property: Description("Repository name for list_prs")]
    string? Repository = null,

    [property: Description("Number of items to return (default 10)")]
    int? Top = null,

    [property: Description("""
        JSON object of fields to update for update_work_item operation.
        Standard fields: System.State, System.Title, Microsoft.VSTS.Scheduling.RemainingWork
        Custom fields: Custom.GitRepository, Custom.YourFieldName
        Example: {"System.State": "Active", "Custom.GitRepository": "edge_subnode"}
        """)]
    string? Fields = null,

    [property: Description("Repository name for get_work_items_by_repo. Filters work items by tag 'repo:xxx'")]
    string? RepoName = null
);
