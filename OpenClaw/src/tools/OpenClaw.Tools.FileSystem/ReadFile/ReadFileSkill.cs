using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.FileSystem.ReadFile;

public class ReadFileSkill : AgentToolBase<ReadFileArgs>
{
    public static ReadFileSkill Default => new();

    private static readonly HashSet<string> SensitiveFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".env.local", ".env.development", ".env.production", ".env.test",
        "credentials", "credentials.json", "secrets", "secrets.json",
        ".npmrc", ".pypirc", ".netrc", ".docker/config.json",
        "id_rsa", "id_ed25519", "id_ecdsa", "id_dsa",
        "known_hosts", "authorized_keys",
        "appsettings.secrets.json", "appsettings.local.json",
        "aws_credentials", "gcloud_credentials",
        ".git-credentials", ".gitconfig"
    };

    private static readonly HashSet<string> SensitivePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "apikey", "api_key", "api-key",
        "secret", "token", "credential", "private_key"
    };

    public override string Name => "read_file";
    public override string Description => "Read the contents of a file at the specified path. Sensitive files (.env, credentials, secrets, keys) are blocked for security.";

    public override async Task<ToolResult> ExecuteAsync(ReadFileArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Path))
        {
            return ToolResult.Failure("Path is required.");
        }

        // Path traversal protection
        var pathError = PathSecurity.ValidatePath(args.Path);
        if (pathError is not null)
        {
            return ToolResult.Failure(pathError);
        }

        if (!File.Exists(args.Path))
        {
            return ToolResult.Failure($"File not found: {args.Path}");
        }

        // Check if file is sensitive (using the resolved full path's filename)
        var fileName = Path.GetFileName(Path.GetFullPath(args.Path));
        if (IsSensitiveFile(fileName))
        {
            return ToolResult.Failure($"Access denied: '{fileName}' is a sensitive file and cannot be read for security reasons.");
        }

        var content = await File.ReadAllTextAsync(args.Path, ct);
        return ToolResult.Success(content);
    }

    private static bool IsSensitiveFile(string fileName)
    {
        // Check exact match
        if (SensitiveFileNames.Contains(fileName))
        {
            return true;
        }

        // Check patterns
        var lowerName = fileName.ToLowerInvariant();
        return SensitivePatterns.Any(pattern => lowerName.Contains(pattern));
    }
}

public record ReadFileArgs(
    [property: Description("The file path to read")]
    string? Path
);
