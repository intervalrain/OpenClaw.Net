using System.Text.Json.Serialization;

namespace OpenClaw.Application.Updates;

public record GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = "";

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    /// <summary>
    /// Parses the version from the tag name (strips leading 'v').
    /// Returns null if parsing fails.
    /// </summary>
    public Version? GetVersion()
    {
        var tag = TagName.TrimStart('v', 'V');
        return Version.TryParse(tag, out var v) ? v : null;
    }
}
