using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.FileSystem.ReadFile;

public class ReadFileSkill : AgentSkillBase<ReadFileArgs>
{
    public static readonly ReadFileSkill Default = new();

    public override string Name => "read_file";
    public override string Description => "Read the contents of a file at the specified path.";

    protected override async Task<SkillResult> ExecuteAsync(ReadFileArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Path))
        {
            return SkillResult.Failure("Path is required.");
        }

        if (!File.Exists(args.Path))
        {
            return SkillResult.Failure($"File not found: {args.Path}");
        }

        var content = await File.ReadAllTextAsync(args.Path, ct);
        return SkillResult.Success(content);
    }
}

public record ReadFileArgs(
    [property: Description("The file path to read")]
    string? Path
);
