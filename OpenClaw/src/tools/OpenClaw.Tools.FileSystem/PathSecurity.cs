namespace OpenClaw.Tools.FileSystem;

/// <summary>
/// Shared path security validation for file system tools.
/// Prevents path traversal attacks by ensuring paths stay within allowed base directories.
/// </summary>
public static class PathSecurity
{
    private static readonly string[] BlockedPathSegments =
    [
        "..", "~"
    ];

    private static readonly HashSet<string> SensitiveDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "/etc", "/var/log", "/proc", "/sys", "/root",
        "C:\\Windows", "C:\\Users", "C:\\ProgramData"
    };

    /// <summary>
    /// Validates that a path does not contain traversal sequences and does not target sensitive directories.
    /// Returns null if valid, or an error message if blocked.
    /// </summary>
    public static string? ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null; // Let caller handle null

        // Block path traversal sequences
        var normalized = path.Replace('\\', '/');
        foreach (var blocked in BlockedPathSegments)
        {
            if (normalized.Contains(blocked))
            {
                return $"Path contains blocked sequence '{blocked}'. Path traversal is not allowed.";
            }
        }

        // Resolve the full path and check against sensitive directories
        try
        {
            var fullPath = Path.GetFullPath(path);
            foreach (var sensitive in SensitiveDirectories)
            {
                if (fullPath.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Access to '{sensitive}' is not allowed for security reasons.";
                }
            }
        }
        catch (Exception ex)
        {
            return $"Invalid path: {ex.Message}";
        }

        return null; // Path is valid
    }
}
