using System.ComponentModel;
using System.Text;

using UglyToad.PdfPig;

using ClawOS.Contracts.Skills;

namespace ClawOS.Tools.Pdf.PdfProcessing;

public class PdfSkill : AgentToolBase<PdfSkillArgs>
{
    public override string Name => "pdf";
    public override string Description => """
        PDF processing skill. Use when: reading PDF text content, extracting metadata,
        counting pages, or searching for text in PDF files.
        """;

    public override async Task<ToolResult> ExecuteAsync(PdfSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.FilePath))
            return ToolResult.Failure("filePath is required.");

        if (!File.Exists(args.FilePath))
            return ToolResult.Failure($"File not found: {args.FilePath}");

        if (!args.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return ToolResult.Failure("File must be a PDF (.pdf extension).");

        try
        {
            var result = args.Operation.ToLowerInvariant() switch
            {
                "read" or "extract_text" => await ExtractTextAsync(args.FilePath, args.StartPage, args.EndPage, args.MaxChars, ct),
                "metadata" or "info" => await GetMetadataAsync(args.FilePath, ct),
                "page_count" or "count_pages" => await GetPageCountAsync(args.FilePath, ct),
                "page" or "get_page" => await GetPageTextAsync(args.FilePath, args.PageNumber, ct),
                "search" => await SearchTextAsync(args.FilePath, args.SearchTerm, ct),
                _ => ToolResult.Failure($"Unknown operation: {args.Operation}. Valid: read, metadata, page_count, page, search")
            };

            return result;
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"PDF processing error: {ex.Message}");
        }
    }

    private static Task<ToolResult> ExtractTextAsync(
        string filePath, int? startPage, int? endPage, int? maxChars, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var document = PdfDocument.Open(filePath);
            var sb = new StringBuilder();
            var totalPages = document.NumberOfPages;

            var start = Math.Max(1, startPage ?? 1);
            var end = Math.Min(totalPages, endPage ?? totalPages);
            var charLimit = maxChars ?? 100000;

            sb.AppendLine($"# PDF Text Extraction");
            sb.AppendLine($"File: {Path.GetFileName(filePath)}");
            sb.AppendLine($"Pages: {start} to {end} of {totalPages}");
            sb.AppendLine("---");
            sb.AppendLine();

            for (int i = start; i <= end; i++)
            {
                ct.ThrowIfCancellationRequested();

                var page = document.GetPage(i);
                var text = page.Text;

                sb.AppendLine($"## Page {i}");
                sb.AppendLine();
                sb.AppendLine(text);
                sb.AppendLine();

                if (sb.Length > charLimit)
                {
                    sb.AppendLine($"... (truncated at {charLimit} chars, total pages: {totalPages})");
                    break;
                }
            }

            return ToolResult.Success(sb.ToString());
        }, ct);
    }

    private static Task<ToolResult> GetMetadataAsync(string filePath, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var document = PdfDocument.Open(filePath);
            var info = document.Information;

            var sb = new StringBuilder();
            sb.AppendLine("# PDF Metadata");
            sb.AppendLine($"File: {Path.GetFileName(filePath)}");
            sb.AppendLine($"Size: {new FileInfo(filePath).Length / 1024.0:F2} KB");
            sb.AppendLine($"Pages: {document.NumberOfPages}");
            sb.AppendLine($"PDF Version: {document.Version}");
            sb.AppendLine();
            sb.AppendLine("## Document Info");
            sb.AppendLine($"- Title: {info.Title ?? "(not set)"}");
            sb.AppendLine($"- Author: {info.Author ?? "(not set)"}");
            sb.AppendLine($"- Subject: {info.Subject ?? "(not set)"}");
            sb.AppendLine($"- Keywords: {info.Keywords ?? "(not set)"}");
            sb.AppendLine($"- Creator: {info.Creator ?? "(not set)"}");
            sb.AppendLine($"- Producer: {info.Producer ?? "(not set)"}");
            sb.AppendLine($"- Creation Date: {info.CreationDate?.ToString() ?? "(not set)"}");
            sb.AppendLine($"- Modified Date: {info.ModifiedDate?.ToString() ?? "(not set)"}");

            return ToolResult.Success(sb.ToString());
        }, ct);
    }

    private static Task<ToolResult> GetPageCountAsync(string filePath, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var document = PdfDocument.Open(filePath);
            return ToolResult.Success($"Page count: {document.NumberOfPages}");
        }, ct);
    }

    private static Task<ToolResult> GetPageTextAsync(string filePath, int? pageNumber, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (pageNumber == null || pageNumber < 1)
                return ToolResult.Failure("pageNumber is required and must be >= 1.");

            using var document = PdfDocument.Open(filePath);

            if (pageNumber > document.NumberOfPages)
                return ToolResult.Failure($"Page {pageNumber} does not exist. Document has {document.NumberOfPages} pages.");

            var page = document.GetPage(pageNumber.Value);
            var sb = new StringBuilder();
            sb.AppendLine($"# Page {pageNumber} of {document.NumberOfPages}");
            sb.AppendLine($"File: {Path.GetFileName(filePath)}");
            sb.AppendLine($"Dimensions: {page.Width:F0} x {page.Height:F0}");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(page.Text);

            return ToolResult.Success(sb.ToString());
        }, ct);
    }

    private static Task<ToolResult> SearchTextAsync(string filePath, string? searchTerm, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return ToolResult.Failure("searchTerm is required for search operation.");

            using var document = PdfDocument.Open(filePath);
            var results = new List<(int Page, string Context)>();

            for (int i = 1; i <= document.NumberOfPages; i++)
            {
                ct.ThrowIfCancellationRequested();

                var page = document.GetPage(i);
                var text = page.Text;

                if (text.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    var index = 0;
                    while ((index = text.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        var contextStart = Math.Max(0, index - 50);
                        var contextEnd = Math.Min(text.Length, index + searchTerm.Length + 50);
                        var context = text[contextStart..contextEnd].Replace("\n", " ").Replace("\r", "");

                        if (contextStart > 0) context = "..." + context;
                        if (contextEnd < text.Length) context += "...";

                        results.Add((i, context));
                        index += searchTerm.Length;

                        if (results.Count >= 50) break;
                    }
                }

                if (results.Count >= 50) break;
            }

            if (results.Count == 0)
                return ToolResult.Success($"No matches found for '{searchTerm}' in the document.");

            var sb = new StringBuilder();
            sb.AppendLine($"# Search Results for '{searchTerm}'");
            sb.AppendLine($"File: {Path.GetFileName(filePath)}");
            sb.AppendLine($"Found: {results.Count} occurrence(s)");
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var (page, context) in results)
            {
                sb.AppendLine($"**Page {page}:** {context}");
                sb.AppendLine();
            }

            return ToolResult.Success(sb.ToString());
        }, ct);
    }
}

public record PdfSkillArgs(
    [property: Description("Path to the PDF file")]
    string? FilePath,

    [property: Description("""
        Operation to perform. Options:
        - 'read' or 'extract_text': Extract text from the PDF
        - 'metadata' or 'info': Get document metadata
        - 'page_count': Get the number of pages
        - 'page' or 'get_page': Get text from a specific page (requires pageNumber)
        - 'search': Search for text in the PDF (requires searchTerm)
        """)]
    string Operation = "read",

    [property: Description("Starting page number for text extraction (1-indexed, default: 1)")]
    int? StartPage = null,

    [property: Description("Ending page number for text extraction (default: last page)")]
    int? EndPage = null,

    [property: Description("Maximum characters to extract (default: 100000)")]
    int? MaxChars = null,

    [property: Description("Page number for get_page operation (1-indexed)")]
    int? PageNumber = null,

    [property: Description("Search term for search operation")]
    string? SearchTerm = null
);
