using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.FileSystem.ListDirectory;

public class ListDirectorySkill : AgentSkillBase<ListDirectoryArgs>
{
    public static ListDirectorySkill Default => new();

    public override string Name => "list_directory";
    public override string Description => "List files and directories at the specified path.";

    public override Task<SkillResult> ExecuteAsync(ListDirectoryArgs args, CancellationToken ct)
    {
        var path = args.Path ?? ".";

        if (!Directory.Exists(path))
        {
            return Task.FromResult(SkillResult.Failure($"Directory not found: {path}"));
        }

        var entries = Directory.GetFileSystemEntries(path)
            .Select(e => Path.GetFileName(e))
            .Order();

        return Task.FromResult(SkillResult.Success(string.Join("\n", entries)));
    }
}

public record ListDirectoryArgs(
    [property: Description("The directory path to list. Defaults to current directory if not specified.")]
    string? Path
);