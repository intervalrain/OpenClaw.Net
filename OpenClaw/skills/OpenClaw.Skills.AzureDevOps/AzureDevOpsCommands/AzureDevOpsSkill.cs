using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.AzureDevOps.AzureDevOpsCommands;

public class AzureDevOpsSkill(IServiceProvider serviceProvider) : AgentSkillBase<AzureDevOpsSkillArgs>
{
    private const string BaseUrl = "https://dev.azure.com";

    public override string Name => "azure_devops";
    public override string Description => """
        Azure DevOps operations. Use when: querying my work items in current sprint,
        listing repositories, checking builds/pipelines, or getting work item details.
        Requires AZURE_DEVOPS_PAT and AZURE_DEVOPS_ORG environment variables.
        """;

    public override async Task<SkillResult> ExecuteAsync(AzureDevOpsSkillArgs args, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();

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
            Console.WriteLine($"[AzureDevOpsSkill] Error reading from config store: {ex.Message}");
        }

        var patFromEnv = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
        var pat = patFromConfig ?? patFromEnv;

        var orgFromEnv = Environment.GetEnvironmentVariable("AZURE_DEVOPS_ORG");
        var org = args.Organization ?? orgFromConfig ?? orgFromEnv;

        // Debug logging
        Console.WriteLine($"[AzureDevOpsSkill] PAT source: {(patFromConfig != null ? "config" : patFromEnv != null ? "env" : "none")}");
        Console.WriteLine($"[AzureDevOpsSkill] ORG: {org ?? "null"}");
        Console.WriteLine($"[AzureDevOpsSkill] Project: {args.Project ?? "null"}");

        if (string.IsNullOrWhiteSpace(pat))
            return SkillResult.Failure("AZURE_DEVOPS_PAT is not set. Configure it via settings or environment variable.");

        if (string.IsNullOrWhiteSpace(org))
            return SkillResult.Failure("Organization is required. Set AZURE_DEVOPS_ORG or provide 'organization' parameter.");

        if (string.IsNullOrWhiteSpace(args.Project))
            return SkillResult.Failure("Project is required.");

        using var client = CreateClient(pat);

        try
        {
            return args.Operation.ToLowerInvariant() switch
            {
                "my_work_items" => await GetMyWorkItemsAsync(client, org, args.Project, args.AssignedTo, ct),
                "list_repos" => await ListReposAsync(client, org, args.Project, ct),
                "list_iterations" => await ListIterationsAsync(client, org, args.Project, args.Team, ct),
                "get_work_item" => await GetWorkItemAsync(client, org, args.Project, args.WorkItemId, ct),
                "list_builds" => await ListBuildsAsync(client, org, args.Project, args.Top, ct),
                "list_pipelines" => await ListPipelinesAsync(client, org, args.Project, ct),
                "list_prs" => await ListPullRequestsAsync(client, org, args.Project, args.Repository, ct),
                "run_query" => await RunSavedQueryAsync(client, org, args.Project, args.QueryId, ct),
                _ => SkillResult.Failure($"Unknown operation: {args.Operation}. Valid: my_work_items, list_repos, list_iterations, get_work_item, list_builds, list_pipelines, list_prs, run_query")
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
    }

    private static HttpClient CreateClient(string pat)
    {
        var client = new HttpClient();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static async Task<SkillResult> GetMyWorkItemsAsync(
        HttpClient client, string org, string project, string? assignedTo, CancellationToken ct)
    {
        var assignee = string.IsNullOrWhiteSpace(assignedTo) ? "@Me" : $"'{assignedTo}'";

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

        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/wit/wiql?api-version=7.1";
        var content = new StringContent(JsonSerializer.Serialize(new { query = wiql }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        var wiqlResult = JsonSerializer.Deserialize<WiqlResponse>(data, JsonOptions);
        if (wiqlResult?.WorkItems == null || wiqlResult.WorkItems.Count == 0)
            return SkillResult.Success("No work items found for current iteration.");

        var ids = wiqlResult.WorkItems.Take(50).Select(w => w.Id);
        var detailsUrl = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/wit/workitems?ids={string.Join(",", ids)}&fields=System.Id,System.Title,System.State,System.WorkItemType,System.AssignedTo,Microsoft.VSTS.Scheduling.RemainingWork&api-version=7.1";

        var detailsResponse = await client.GetAsync(detailsUrl, ct);
        var detailsData = await detailsResponse.Content.ReadAsStringAsync(ct);

        if (!detailsResponse.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({detailsResponse.StatusCode}): {detailsData}");

        return SkillResult.Success(FormatJson(detailsData));
    }

    private static async Task<SkillResult> ListReposAsync(
        HttpClient client, string org, string project, CancellationToken ct)
    {
        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/git/repositories?api-version=7.1";

        var response = await client.GetAsync(url, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private static async Task<SkillResult> ListIterationsAsync(
        HttpClient client, string org, string project, string? team, CancellationToken ct)
    {
        var teamName = team ?? $"{project} Team";
        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(teamName)}/_apis/work/teamsettings/iterations?api-version=7.1";

        var response = await client.GetAsync(url, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private static async Task<SkillResult> GetWorkItemAsync(
        HttpClient client, string org, string project, int? workItemId, CancellationToken ct)
    {
        if (workItemId == null)
            return SkillResult.Failure("workItemId is required.");

        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/wit/workitems/{workItemId}?$expand=all&api-version=7.1";

        var response = await client.GetAsync(url, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private static async Task<SkillResult> ListBuildsAsync(
        HttpClient client, string org, string project, int? top, CancellationToken ct)
    {
        var topCount = top ?? 10;
        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/build/builds?$top={topCount}&api-version=7.1";

        var response = await client.GetAsync(url, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private static async Task<SkillResult> ListPipelinesAsync(
        HttpClient client, string org, string project, CancellationToken ct)
    {
        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/pipelines?api-version=7.1";

        var response = await client.GetAsync(url, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private static async Task<SkillResult> ListPullRequestsAsync(
        HttpClient client, string org, string project, string? repository, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repository))
            return SkillResult.Failure("repository is required for list_prs operation.");

        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repository)}/pullrequests?searchCriteria.status=active&api-version=7.1";

        var response = await client.GetAsync(url, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
    }

    private static async Task<SkillResult> RunSavedQueryAsync(
        HttpClient client, string org, string project, string? queryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryId))
            return SkillResult.Failure("queryId is required.");

        var url = $"{BaseUrl}/{org}/{Uri.EscapeDataString(project)}/_apis/wit/wiql/{queryId}?api-version=7.1";

        var response = await client.GetAsync(url, ct);
        var data = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return SkillResult.Failure($"API error ({response.StatusCode}): {data}");

        return SkillResult.Success(FormatJson(data));
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
        - my_work_items: Get my work items in current iteration (optional assignedTo)
        - list_repos: List all repositories in the project
        - list_iterations: List iterations/sprints (optional team)
        - get_work_item: Get work item details (requires workItemId)
        - list_builds: List recent builds (optional top)
        - list_pipelines: List all pipelines
        - list_prs: List pull requests (requires repository)
        - run_query: Execute a saved query (requires queryId)
        """)]
    string Operation,

    [property: Description("Azure DevOps project name (e.g., 'IoT Platform')")]
    string Project,

    [property: Description("Azure DevOps organization name. Defaults to AZURE_DEVOPS_ORG env var")]
    string? Organization = null,

    [property: Description("User display name for my_work_items (e.g., 'Rain Hu'). Defaults to @Me")]
    string? AssignedTo = null,

    [property: Description("Team name for list_iterations. Defaults to '{Project} Team'")]
    string? Team = null,

    [property: Description("Work item ID for get_work_item")]
    int? WorkItemId = null,

    [property: Description("Saved query ID (GUID) for run_query")]
    string? QueryId = null,

    [property: Description("Repository name for list_prs")]
    string? Repository = null,

    [property: Description("Number of items to return (default 10)")]
    int? Top = null
);