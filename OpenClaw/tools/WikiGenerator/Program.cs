using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Markdig;

var wikiDir = args.Length > 0 ? args[0] : "docs/wiki";
var outputDir = args.Length > 1 ? args[1] : Path.Combine(wikiDir, "_generated");

if (!Directory.Exists(wikiDir))
{
    Console.Error.WriteLine($"Wiki directory not found: {wikiDir}");
    return 1;
}

var generator = new WikiGenerator(wikiDir, outputDir);
var result = await generator.GenerateAsync();

Console.WriteLine($"Wiki generation complete: {result.Generated} generated, {result.Skipped} skipped (unchanged)");
return 0;

public class WikiGenerator(string wikiDir, string outputDir)
{
    private readonly string _checksumFile = Path.Combine(outputDir, ".checksums.json");
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoLinks()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseGridTables()
        .UseFootnotes()
        .UseAutoIdentifiers()
        .Build();

    public async Task<(int Generated, int Skipped)> GenerateAsync()
    {
        Directory.CreateDirectory(outputDir);

        var checksums = await LoadChecksumsAsync();
        var newChecksums = new Dictionary<string, string>();
        var generated = 0;
        var skipped = 0;

        var languages = new[] { "en", "zh" };

        foreach (var lang in languages)
        {
            var langDir = Path.Combine(wikiDir, lang);
            if (!Directory.Exists(langDir)) continue;

            var langOutputDir = Path.Combine(outputDir, lang);
            Directory.CreateDirectory(langOutputDir);

            foreach (var mdFile in Directory.GetFiles(langDir, "*.md"))
            {
                var relativePath = Path.GetRelativePath(wikiDir, mdFile);
                var content = await File.ReadAllTextAsync(mdFile);
                var checksum = ComputeChecksum(content);

                newChecksums[relativePath] = checksum;

                var outputFile = Path.Combine(langOutputDir, Path.GetFileNameWithoutExtension(mdFile) + ".html");

                if (checksums.TryGetValue(relativePath, out var existingChecksum) &&
                    existingChecksum == checksum &&
                    File.Exists(outputFile))
                {
                    skipped++;
                    continue;
                }

                var html = ConvertToHtml(content, Path.GetFileNameWithoutExtension(mdFile));
                await File.WriteAllTextAsync(outputFile, html);
                generated++;

                Console.WriteLine($"  Generated: {relativePath}");
            }
        }

        await SaveChecksumsAsync(newChecksums);
        await GenerateManifestAsync(languages);

        return (generated, skipped);
    }

    private string ConvertToHtml(string markdown, string fileName)
    {
        // Remove YAML frontmatter
        var content = markdown;
        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("---", 3);
            if (endIndex > 0)
            {
                var frontmatter = content[3..endIndex].Trim();
                content = content[(endIndex + 3)..].TrimStart();

                // Extract title from frontmatter
                var titleLine = frontmatter.Split('\n')
                    .FirstOrDefault(l => l.StartsWith("title:"));
                if (titleLine != null)
                {
                    // Title could be used for metadata if needed
                }
            }
        }

        var htmlContent = Markdown.ToHtml(content, _pipeline);

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <meta name="generator" content="WikiGenerator">
                <link rel="stylesheet" href="../wiki.css">
                <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/vs2015.min.css">
                <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"></script>
                <script>hljs.highlightAll();</script>
            </head>
            <body>
                <article class="markdown-body">
                {htmlContent}
                </article>
            </body>
            </html>
            """;
    }

    private async Task GenerateManifestAsync(string[] languages)
    {
        var manifest = new Dictionary<string, List<DocInfo>>();

        foreach (var lang in languages)
        {
            var langDir = Path.Combine(wikiDir, lang);
            if (!Directory.Exists(langDir)) continue;

            var docs = new List<DocInfo>();

            foreach (var mdFile in Directory.GetFiles(langDir, "*.md").OrderBy(f => f))
            {
                var fileName = Path.GetFileNameWithoutExtension(mdFile);
                var content = await File.ReadAllTextAsync(mdFile);
                var title = ExtractTitle(content, fileName);

                docs.Add(new DocInfo(fileName, title, $"{lang}/{fileName}.html"));
            }

            manifest[lang] = docs;
        }

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(Path.Combine(outputDir, "manifest.json"), json);
    }

    private static string ExtractTitle(string content, string fallback)
    {
        // Try frontmatter title first
        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("---", 3);
            if (endIndex > 0)
            {
                var frontmatter = content[3..endIndex];
                var titleLine = frontmatter.Split('\n')
                    .FirstOrDefault(l => l.Trim().StartsWith("title:"));
                if (titleLine != null)
                {
                    return titleLine.Split(':', 2)[1].Trim();
                }
            }
        }

        // Try first H1
        var lines = content.Split('\n');
        var h1Line = lines.FirstOrDefault(l => l.StartsWith("# "));
        if (h1Line != null)
        {
            return h1Line[2..].Trim();
        }

        return fallback;
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16];
    }

    private async Task<Dictionary<string, string>> LoadChecksumsAsync()
    {
        if (!File.Exists(_checksumFile))
            return [];

        var json = await File.ReadAllTextAsync(_checksumFile);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }

    private async Task SaveChecksumsAsync(Dictionary<string, string> checksums)
    {
        var json = JsonSerializer.Serialize(checksums, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_checksumFile, json);
    }
}

public record DocInfo(string Id, string Title, string Path);
