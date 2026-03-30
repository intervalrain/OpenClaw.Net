using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.FileSystem.WriteFile;

public class WriteFileSkill : AgentToolBase<WriteFileArgs>
{
    public static WriteFileSkill Default => new();

    private static readonly HashSet<string> SensitiveFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".env.local", ".env.development", ".env.production",
        "credentials", "credentials.json", "secrets", "secrets.json",
        ".npmrc", ".pypirc", ".netrc", ".docker/config.json",
        "id_rsa", "id_ed25519", "id_ecdsa", "id_dsa",
        "appsettings.secrets.json", "appsettings.local.json",
        ".git-credentials", ".gitconfig"
    };

    public override string Name => "write_file";
    public override string Description => "Write content to a file at the specified path. Creates the file if it doesn't exist.";

    public override async Task<ToolResult> ExecuteAsync(WriteFileArgs args, ToolContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Path))
            return ToolResult.Failure("Path is required.");

        var userId = context.UserId ?? Guid.Empty;
        var resolvedPath = PathSecurity.ResolveUserPath(args.Path, userId, context.IsSuperAdmin);

        // Workspace boundary check
        var pathError = PathSecurity.ValidatePath(resolvedPath, userId, context.IsSuperAdmin);
        if (pathError is not null)
            return ToolResult.Failure(pathError);

        // Block writing to shared workspace (read-only for non-SuperAdmin)
        if (!context.IsSuperAdmin && PathSecurity.IsSharedPath(resolvedPath))
            return ToolResult.Failure("The shared workspace is read-only.");

        // Block writing to sensitive files
        var fileName = Path.GetFileName(resolvedPath);
        if (SensitiveFileNames.Contains(fileName))
            return ToolResult.Failure($"Writing to '{fileName}' is not allowed for security reasons.");

        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(resolvedPath, args.Content ?? "", ct);
        return ToolResult.Success($"File written successfully: {args.Path}");
    }
}

public record WriteFileArgs(
    [property: Description("The file path to write to")]
    string? Path,
    [property: Description("The content to write to the file")]
    string? Content
);
