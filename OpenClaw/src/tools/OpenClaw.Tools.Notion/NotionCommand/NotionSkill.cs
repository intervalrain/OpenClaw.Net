using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.Notion.NotionCommand;

/// <summary>
/// Notion operations via Notion REST API: pages, databases, search, comments.
/// Requires: NOTION_API_TOKEN configured in Settings > Configs.
/// </summary>
public class NotionSkill(
    IServiceProvider serviceProvider,
    ILogger<NotionSkill> logger) : AgentToolBase<NotionSkillArgs>
{
    private const string NotionApiVersion = "2022-06-28";
    private const string BaseUrl = "https://api.notion.com/v1";

    public override string Name => "notion";
    public override string Description => """
        Notion operations: search content, read/create pages, query databases, manage comments.
        Operations: search, get_page, create_page, update_page, query_database, list_databases, get_database, add_comment, get_block_children.
        Requires NOTION_API_TOKEN configured in Settings > Configs.
        """;

    public override async Task<ToolResult> ExecuteAsync(NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Operation))
        {
            return ToolResult.Failure(
                "Operation is required. Valid operations: search, get_page, create_page, update_page, " +
                "query_database, list_databases, get_database, add_comment, get_block_children");
        }

        using var scope = serviceProvider.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();
        var token = configStore.Get(ConfigKeys.NotionApiToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            return ToolResult.Failure(
                "NOTION_API_TOKEN is not configured. Set it in Settings > Configs with key 'NOTION_API_TOKEN'. " +
                "Create an integration at https://www.notion.so/my-integrations");
        }

        using var client = CreateClient(token);

        try
        {
            return args.Operation.ToLowerInvariant() switch
            {
                "search" => await SearchAsync(client, args, ct),
                "get_page" => await GetPageAsync(client, args, ct),
                "create_page" => await CreatePageAsync(client, args, ct),
                "update_page" => await UpdatePageAsync(client, args, ct),
                "create_database" => await CreateDatabaseAsync(client, args, ct),
                "query_database" => await QueryDatabaseAsync(client, args, ct),
                "list_databases" => await ListDatabasesAsync(client, ct),
                "get_database" => await GetDatabaseAsync(client, args, ct),
                "add_comment" => await AddCommentAsync(client, args, ct),
                "get_block_children" => await GetBlockChildrenAsync(client, args, ct),
                _ => ToolResult.Failure(
                    $"Unknown operation: {args.Operation}. Valid: search, get_page, create_page, " +
                    "update_page, create_database, query_database, list_databases, get_database, add_comment, get_block_children")
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Notion API HTTP error");
            return ToolResult.Failure($"Notion API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notion skill error");
            return ToolResult.Failure($"Error: {ex.Message}");
        }
    }

    private static HttpClient CreateClient(string token)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Notion-Version", NotionApiVersion);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static async Task<ToolResult> SearchAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        var body = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(args.Query))
            body["query"] = args.Query;

        if (!string.IsNullOrWhiteSpace(args.Filter))
        {
            body["filter"] = new Dictionary<string, string>
            {
                ["value"] = args.Filter.ToLowerInvariant(),
                ["property"] = "object"
            };
        }

        body["page_size"] = args.Limit ?? 10;

        var response = await PostAsync(client, $"{BaseUrl}/search", body, ct);
        return response;
    }

    private static async Task<ToolResult> GetPageAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.PageId))
            return ToolResult.Failure("PageId is required for get_page operation.");

        var pageId = NormalizeId(args.PageId);
        return await GetAsync(client, $"{BaseUrl}/pages/{pageId}", ct);
    }

    private static async Task<ToolResult> CreatePageAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.ParentId))
            return ToolResult.Failure("ParentId (page or database ID) is required for create_page operation.");

        if (string.IsNullOrWhiteSpace(args.Title))
            return ToolResult.Failure("Title is required for create_page operation.");

        var parentId = NormalizeId(args.ParentId);

        var body = new Dictionary<string, object>();

        // Set parent
        if (args.ParentType?.ToLowerInvariant() == "database")
        {
            body["parent"] = new Dictionary<string, string> { ["database_id"] = parentId };
            body["properties"] = new Dictionary<string, object>
            {
                ["Name"] = new Dictionary<string, object>
                {
                    ["title"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["text"] = new Dictionary<string, string> { ["content"] = args.Title }
                        }
                    }
                }
            };
        }
        else
        {
            body["parent"] = new Dictionary<string, string> { ["page_id"] = parentId };
            body["properties"] = new Dictionary<string, object>
            {
                ["title"] = new Dictionary<string, object>
                {
                    ["title"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["text"] = new Dictionary<string, string> { ["content"] = args.Title }
                        }
                    }
                }
            };
        }

        // Add content if provided
        if (!string.IsNullOrWhiteSpace(args.Content))
        {
            body["children"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["object"] = "block",
                    ["type"] = "paragraph",
                    ["paragraph"] = new Dictionary<string, object>
                    {
                        ["rich_text"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "text",
                                ["text"] = new Dictionary<string, string> { ["content"] = args.Content }
                            }
                        }
                    }
                }
            };
        }

        return await PostAsync(client, $"{BaseUrl}/pages", body, ct);
    }

    private static async Task<ToolResult> UpdatePageAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.PageId))
            return ToolResult.Failure("PageId is required for update_page operation.");

        var pageId = NormalizeId(args.PageId);
        var body = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(args.Title))
        {
            body["properties"] = new Dictionary<string, object>
            {
                ["title"] = new Dictionary<string, object>
                {
                    ["title"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["text"] = new Dictionary<string, string> { ["content"] = args.Title }
                        }
                    }
                }
            };
        }

        if (args.Archive.HasValue)
        {
            body["archived"] = args.Archive.Value;
        }

        return await PatchAsync(client, $"{BaseUrl}/pages/{pageId}", body, ct);
    }

    private static async Task<ToolResult> CreateDatabaseAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.ParentId))
            return ToolResult.Failure("ParentId (page ID) is required for create_database operation.");

        if (string.IsNullOrWhiteSpace(args.Title))
            return ToolResult.Failure("Title is required for create_database operation.");

        var parentId = NormalizeId(args.ParentId);

        // Build database schema with default Name property
        var properties = new Dictionary<string, object>
        {
            ["Name"] = new Dictionary<string, object>
            {
                ["title"] = new Dictionary<string, object>()
            }
        };

        // Add optional properties based on schema parameter
        if (!string.IsNullOrWhiteSpace(args.SchemaJson))
        {
            try
            {
                var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(args.SchemaJson);
                if (schema != null)
                {
                    foreach (var prop in schema)
                    {
                        properties[prop.Key] = prop.Value;
                    }
                }
            }
            catch (JsonException ex)
            {
                return ToolResult.Failure($"Invalid schema JSON: {ex.Message}");
            }
        }

        var body = new Dictionary<string, object>
        {
            ["parent"] = new Dictionary<string, string> { ["page_id"] = parentId },
            ["title"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = new Dictionary<string, string> { ["content"] = args.Title }
                }
            },
            ["properties"] = properties
        };

        return await PostAsync(client, $"{BaseUrl}/databases", body, ct);
    }

    private static async Task<ToolResult> QueryDatabaseAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.DatabaseId))
            return ToolResult.Failure("DatabaseId is required for query_database operation.");

        var databaseId = NormalizeId(args.DatabaseId);
        var body = new Dictionary<string, object>
        {
            ["page_size"] = args.Limit ?? 20
        };

        if (!string.IsNullOrWhiteSpace(args.FilterJson))
        {
            try
            {
                var filter = JsonSerializer.Deserialize<JsonElement>(args.FilterJson);
                body["filter"] = filter;
            }
            catch (JsonException ex)
            {
                return ToolResult.Failure($"Invalid filter JSON: {ex.Message}");
            }
        }

        return await PostAsync(client, $"{BaseUrl}/databases/{databaseId}/query", body, ct);
    }

    private static async Task<ToolResult> ListDatabasesAsync(HttpClient client, CancellationToken ct)
    {
        var body = new Dictionary<string, object>
        {
            ["filter"] = new Dictionary<string, string>
            {
                ["value"] = "database",
                ["property"] = "object"
            },
            ["page_size"] = 50
        };

        return await PostAsync(client, $"{BaseUrl}/search", body, ct);
    }

    private static async Task<ToolResult> GetDatabaseAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.DatabaseId))
            return ToolResult.Failure("DatabaseId is required for get_database operation.");

        var databaseId = NormalizeId(args.DatabaseId);
        return await GetAsync(client, $"{BaseUrl}/databases/{databaseId}", ct);
    }

    private static async Task<ToolResult> AddCommentAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.PageId))
            return ToolResult.Failure("PageId is required for add_comment operation.");

        if (string.IsNullOrWhiteSpace(args.Content))
            return ToolResult.Failure("Content is required for add_comment operation.");

        var pageId = NormalizeId(args.PageId);
        var body = new Dictionary<string, object>
        {
            ["parent"] = new Dictionary<string, string> { ["page_id"] = pageId },
            ["rich_text"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["text"] = new Dictionary<string, string> { ["content"] = args.Content }
                }
            }
        };

        return await PostAsync(client, $"{BaseUrl}/comments", body, ct);
    }

    private static async Task<ToolResult> GetBlockChildrenAsync(HttpClient client, NotionSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.PageId) && string.IsNullOrWhiteSpace(args.BlockId))
            return ToolResult.Failure("PageId or BlockId is required for get_block_children operation.");

        var blockId = NormalizeId(args.BlockId ?? args.PageId!);
        var pageSize = args.Limit ?? 50;

        return await GetAsync(client, $"{BaseUrl}/blocks/{blockId}/children?page_size={pageSize}", ct);
    }

    private static async Task<ToolResult> GetAsync(HttpClient client, string url, CancellationToken ct)
    {
        var response = await client.GetAsync(url, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ToolResult.Failure($"API error ({response.StatusCode}): {TruncateError(content)}");
        }

        return ToolResult.Success(FormatJson(content));
    }

    private static async Task<ToolResult> PostAsync(HttpClient client, string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ToolResult.Failure($"API error ({response.StatusCode}): {TruncateError(responseContent)}");
        }

        return ToolResult.Success(FormatJson(responseContent));
    }

    private static async Task<ToolResult> PatchAsync(HttpClient client, string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await client.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ToolResult.Failure($"API error ({response.StatusCode}): {TruncateError(responseContent)}");
        }

        return ToolResult.Success(FormatJson(responseContent));
    }

    private static string NormalizeId(string id)
    {
        // Remove hyphens if present (Notion accepts both formats)
        return id.Replace("-", "");
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    private static string TruncateError(string error)
    {
        const int maxLength = 500;
        return error.Length > maxLength ? error[..maxLength] + "..." : error;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public record NotionSkillArgs(
    [property: Description("""
        The operation to perform:
        - search: Search for pages or databases by query
        - get_page: Get page details by ID
        - create_page: Create a new page under a parent page or database
        - update_page: Update page title or archive status
        - create_database: Create a new database under a parent page
        - query_database: Query a database with optional filters
        - list_databases: List all accessible databases
        - get_database: Get database schema and properties
        - add_comment: Add a comment to a page
        - get_block_children: Get content blocks of a page
        """)]
    string Operation,

    [property: Description("Search query text (for search operation)")]
    string? Query = null,

    [property: Description("Filter by object type: 'page' or 'database' (for search operation)")]
    string? Filter = null,

    [property: Description("Page ID (for get_page, update_page, add_comment, get_block_children)")]
    string? PageId = null,

    [property: Description("Database ID (for query_database, get_database)")]
    string? DatabaseId = null,

    [property: Description("Block ID (for get_block_children, alternative to pageId)")]
    string? BlockId = null,

    [property: Description("Parent page or database ID (for create_page, create_database)")]
    string? ParentId = null,

    [property: Description("Parent type: 'page' or 'database' (for create_page, default: page)")]
    string? ParentType = null,

    [property: Description("Page or database title (for create_page, create_database, update_page)")]
    string? Title = null,

    [property: Description("Page content text (for create_page) or comment text (for add_comment)")]
    string? Content = null,

    [property: Description("Archive the page (for update_page)")]
    bool? Archive = null,

    [property: Description("Database filter as JSON string (for query_database). Example: {\"property\":\"Status\",\"select\":{\"equals\":\"Done\"}}")]
    string? FilterJson = null,

    [property: Description("Database schema as JSON (for create_database). Example: {\"Status\":{\"select\":{\"options\":[{\"name\":\"Todo\"},{\"name\":\"Done\"}]}}}")]
    string? SchemaJson = null,

    [property: Description("Maximum number of results to return (default: 10-50 depending on operation)")]
    int? Limit = null
);
