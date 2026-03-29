using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.FileSystem.ListDirectory;

public class ListDirectorySkill : AgentToolBase<ListDirectoryArgs>
{
    public static ListDirectorySkill Default => new();

    public override string Name => "list_directory";
    public override string Description => "List files and directories at the specified path.";

    public override Task<ToolResult> ExecuteAsync(ListDirectoryArgs args, CancellationToken ct)
    {
        var path = args.Path ?? ".";

        // Path traversal protection
        var pathError = PathSecurity.ValidatePath(path);
        if (pathError is not null)
        {
            return Task.FromResult(ToolResult.Failure(pathError));
        }

        if (!Directory.Exists(path))
        {
            return Task.FromResult(ToolResult.Failure($"Directory not found: {path}"));
        }

        var entries = Directory.GetFileSystemEntries(path)
            .Select(e => Path.GetFileName(e))
            .Order();

        return Task.FromResult(ToolResult.Success(string.Join("\n", entries)));
    }
}

public record ListDirectoryArgs(
    [property: Description("The directory path to list. Defaults to current directory if not specified.")]
    string? Path
);
