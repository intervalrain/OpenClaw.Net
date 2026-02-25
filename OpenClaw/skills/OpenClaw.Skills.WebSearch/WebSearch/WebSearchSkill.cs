using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.WebSearch.WebSearch;

public class WebSearchSkill(string? searxngUrl = null, TimeSpan? timeout = null) : AgentSkillBase<WebSearchArgs>
{
    public static WebSearchSkill Default => new(
        searxngUrl: Environment.GetEnvironmentVariable("SEARXNG_URL") ?? "http://localhost:8080",
        timeout: TimeSpan.FromSeconds(10));

    private readonly string _searxngUrl = searxngUrl ?? "http://localhost:8080";
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    public override string Name => "web_search";
    public override string Description => "Search the web using SearXNG and return relevant results. Use this when you need current information from the internet.";

    public override async Task<SkillResult> ExecuteAsync(WebSearchArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
        {
            return SkillResult.Failure("Search query is required.");
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = _timeout };

            var encodedQuery = HttpUtility.UrlEncode(args.Query);
            var count = args.Count ?? 5;
            var url = $"{_searxngUrl}/search?q={encodedQuery}&format=json&pageno=1";

            var response = await httpClient.GetFromJsonAsync<SearxngResponse>(url, ct);

            if (response is null)
            {
                return SkillResult.Failure("Failed to get search results.");
            }

            var result = FormatResponse(response, args.Query, count);
            return SkillResult.Success(result);
        }
        catch (TaskCanceledException)
        {
            return SkillResult.Failure("Search request timed out.");
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Search failed: {ex.Message}");
        }
    }

    private static string FormatResponse(SearxngResponse response, string query, int count)
    {
        var parts = new List<string> { $"# Search results for: {query}", "" };

        if (response.Results is { Count: > 0 })
        {
            foreach (var (result, index) in response.Results.Take(count).Select((r, i) => (r, i + 1)))
            {
                parts.Add($"## {index}. {result.Title}");
                if (!string.IsNullOrEmpty(result.Content))
                {
                    parts.Add(result.Content);
                }
                if (!string.IsNullOrEmpty(result.Url))
                {
                    parts.Add($"URL: {result.Url}");
                }
                parts.Add("");
            }
        }

        if (parts.Count <= 2)
        {
            parts.Add("No results found. Try rephrasing your search query.");
        }

        return string.Join("\n", parts);
    }
}

public record WebSearchArgs(
    [property: Description("The search query to look up on the web")]
    string? Query,

    [property: Description("Number of results to return (default: 5, max: 10)")]
    int? Count = 5
);

internal class SearxngResponse
{
    [JsonPropertyName("results")]
    public List<SearxngResult>? Results { get; set; }
}

internal class SearxngResult
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}