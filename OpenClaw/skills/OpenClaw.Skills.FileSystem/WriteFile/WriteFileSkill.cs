using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.FileSystem.WriteFile;

public class WriteFileSkill : AgentSkillBase<WriteFileArgs>
{
    public static readonly WriteFileSkill Default = new();

    public override string Name => "write_file";
    public override string Description => "Write content to a file at the specified path. Creates the file if it doesn't exist.";

    protected override async Task<SkillResult> ExecuteAsync(WriteFileArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Path))
        {
            return SkillResult.Failure("Path is required.");
        }

        var directory = Path.GetDirectoryName(args.Path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(args.Path, args.Content ?? "", ct);
        return SkillResult.Success($"File written successfully: {args.Path}");
    }
}

public record WriteFileArgs(
    [property: Description("The file path to write to")]
    string? Path,
    [property: Description("The content to write to the file")]
    string? Content
);