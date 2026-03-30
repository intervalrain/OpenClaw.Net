namespace OpenClaw.Tools.FileSystem;

/// <summary>
/// Per-user workspace filesystem isolation.
///
/// Workspace layout:
///   {basePath}/
///     shared/        — readable by all users (read-only)
///     {userId}/      — private workspace per user
///
/// Rules:
///   - Regular users can only access their own workspace and shared/
///   - SuperAdmin can access all user workspaces
///   - Path traversal (../) is always blocked
///   - Sensitive system directories are always blocked
/// </summary>
public static class PathSecurity
{
    private static readonly string[] BlockedPathSegments = ["..", "~"];

    private static readonly HashSet<string> SensitiveDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "/etc", "/var/log", "/proc", "/sys", "/root", "/dev",
        "C:\\Windows", "C:\\Users", "C:\\ProgramData"
    };

    /// <summary>
    /// Get the workspace base path from environment or default.
    /// </summary>
    public static string GetWorkspaceBasePath()
    {
        return Environment.GetEnvironmentVariable("OPENCLAW_WORKSPACE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "workspace");
    }

    /// <summary>
    /// Get the workspace root for a specific user.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public static string GetUserWorkspacePath(Guid userId)
    {
        var basePath = GetWorkspaceBasePath();
        var userPath = Path.Combine(basePath, userId.ToString());

        if (!Directory.Exists(userPath))
            Directory.CreateDirectory(userPath);

        return userPath;
    }

    /// <summary>
    /// Get the shared workspace path (readable by all users).
    /// </summary>
    public static string GetSharedWorkspacePath()
    {
        var basePath = GetWorkspaceBasePath();
        var sharedPath = Path.Combine(basePath, "shared");

        if (!Directory.Exists(sharedPath))
            Directory.CreateDirectory(sharedPath);

        return sharedPath;
    }

    /// <summary>
    /// Resolve a user-relative path to an absolute path within their workspace.
    /// If the path is already absolute, validates it's within allowed boundaries.
    /// </summary>
    public static string ResolveUserPath(string path, Guid userId, bool isSuperAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return GetUserWorkspacePath(userId);

        // If relative, resolve against user workspace
        if (!Path.IsPathRooted(path))
            return Path.GetFullPath(Path.Combine(GetUserWorkspacePath(userId), path));

        // Absolute path — will be validated by ValidatePath
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Validates that a path is within the user's allowed boundaries.
    /// Returns null if valid, or an error message if blocked.
    /// </summary>
    public static string? ValidatePath(string? path, Guid userId, bool isSuperAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        // Block path traversal sequences
        var normalized = path.Replace('\\', '/');
        foreach (var blocked in BlockedPathSegments)
        {
            if (normalized.Contains(blocked))
                return $"Path contains blocked sequence '{blocked}'. Path traversal is not allowed.";
        }

        // Resolve full path
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return $"Invalid path: {ex.Message}";
        }

        // Block sensitive system directories
        foreach (var sensitive in SensitiveDirectories)
        {
            if (fullPath.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase))
                return $"Access to '{sensitive}' is not allowed for security reasons.";
        }

        var basePath = Path.GetFullPath(GetWorkspaceBasePath());
        var userWorkspace = Path.GetFullPath(GetUserWorkspacePath(userId));
        var sharedWorkspace = Path.GetFullPath(GetSharedWorkspacePath());

        // SuperAdmin can access everything under workspace base
        if (isSuperAdmin)
        {
            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return $"Access denied: path must be within the workspace directory '{basePath}'.";
            return null;
        }

        // Regular users: own workspace (read/write) or shared (read-only validated by caller)
        if (fullPath.StartsWith(userWorkspace, StringComparison.OrdinalIgnoreCase))
            return null;

        if (fullPath.StartsWith(sharedWorkspace, StringComparison.OrdinalIgnoreCase))
            return null;

        return $"Access denied: you can only access your workspace or the shared directory.";
    }

    /// <summary>
    /// Check if a path is in the shared workspace (read-only for regular users).
    /// </summary>
    public static bool IsSharedPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var sharedWorkspace = Path.GetFullPath(GetSharedWorkspacePath());
        return fullPath.StartsWith(sharedWorkspace, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Legacy validation without user context — blocks everything outside workspace.
    /// Used only as a safety net; prefer the userId-aware overload.
    /// </summary>
    public static string? ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.Replace('\\', '/');
        foreach (var blocked in BlockedPathSegments)
        {
            if (normalized.Contains(blocked))
                return $"Path contains blocked sequence '{blocked}'. Path traversal is not allowed.";
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            foreach (var sensitive in SensitiveDirectories)
            {
                if (fullPath.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase))
                    return $"Access to '{sensitive}' is not allowed for security reasons.";
            }
        }
        catch (Exception ex)
        {
            return $"Invalid path: {ex.Message}";
        }

        return null;
    }
}
