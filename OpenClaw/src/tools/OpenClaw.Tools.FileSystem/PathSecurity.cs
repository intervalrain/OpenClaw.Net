namespace OpenClaw.Tools.FileSystem;

/// <summary>
/// Workspace filesystem isolation.
///
/// Workspace layout:
///   {basePath}/
///     shared/              — readable by all users (read-only)
///     {workspaceId}/       — workspace directory (personal or shared)
///
/// Rules:
///   - Users can only access workspaces they are members of + shared/
///   - SuperAdmin can access all workspaces
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

    public static string GetWorkspaceBasePath()
    {
        return Environment.GetEnvironmentVariable("OPENCLAW_WORKSPACE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "workspace");
    }

    /// <summary>
    /// Get the directory for a workspace. Creates if not exists.
    /// </summary>
    public static string GetWorkspacePath(Guid workspaceId)
    {
        var basePath = GetWorkspaceBasePath();
        var wsPath = Path.Combine(basePath, workspaceId.ToString());

        if (!Directory.Exists(wsPath))
            Directory.CreateDirectory(wsPath);

        return wsPath;
    }

    /// <summary>
    /// Backward compatible: get workspace path by userId (personal workspace).
    /// Prefer GetWorkspacePath(workspaceId) for new code.
    /// </summary>
    public static string GetUserWorkspacePath(Guid userId)
        => GetWorkspacePath(userId);

    public static string GetSharedWorkspacePath()
    {
        var basePath = GetWorkspaceBasePath();
        var sharedPath = Path.Combine(basePath, "shared");

        if (!Directory.Exists(sharedPath))
            Directory.CreateDirectory(sharedPath);

        return sharedPath;
    }

    /// <summary>
    /// Resolve a relative path within a workspace to an absolute path.
    /// </summary>
    public static string ResolveWorkspacePath(string path, Guid workspaceId)
    {
        if (string.IsNullOrWhiteSpace(path))
            return GetWorkspacePath(workspaceId);

        if (!Path.IsPathRooted(path))
            return Path.GetFullPath(Path.Combine(GetWorkspacePath(workspaceId), path));

        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Backward compatible: resolve path by userId.
    /// </summary>
    public static string ResolveUserPath(string path, Guid userId, bool isSuperAdmin = false)
        => ResolveWorkspacePath(path, userId);

    /// <summary>
    /// Validate that a path is within the workspace's allowed boundaries.
    /// </summary>
    public static string? ValidateWorkspacePath(string? path, Guid workspaceId, bool isSuperAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var blockError = CheckBlocked(path);
        if (blockError is not null) return blockError;

        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception ex) { return $"Invalid path: {ex.Message}"; }

        var sensitiveError = CheckSensitive(fullPath);
        if (sensitiveError is not null) return sensitiveError;

        var basePath = Path.GetFullPath(GetWorkspaceBasePath());

        if (isSuperAdmin)
        {
            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return $"Access denied: path must be within the workspace directory '{basePath}'.";
            return null;
        }

        var workspace = Path.GetFullPath(GetWorkspacePath(workspaceId));
        if (fullPath.StartsWith(workspace, StringComparison.OrdinalIgnoreCase))
            return null;

        var shared = Path.GetFullPath(GetSharedWorkspacePath());
        if (fullPath.StartsWith(shared, StringComparison.OrdinalIgnoreCase))
            return null;

        return "Access denied: you can only access your workspace or the shared directory.";
    }

    /// <summary>
    /// Backward compatible: validate by userId.
    /// </summary>
    public static string? ValidatePath(string? path, Guid userId, bool isSuperAdmin = false)
        => ValidateWorkspacePath(path, userId, isSuperAdmin);

    public static bool IsSharedPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var sharedWorkspace = Path.GetFullPath(GetSharedWorkspacePath());
        return fullPath.StartsWith(sharedWorkspace, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Legacy validation without user context.
    /// </summary>
    public static string? ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var blockError = CheckBlocked(path);
        if (blockError is not null) return blockError;

        try
        {
            var fullPath = Path.GetFullPath(path);
            return CheckSensitive(fullPath);
        }
        catch (Exception ex)
        {
            return $"Invalid path: {ex.Message}";
        }
    }

    private static string? CheckBlocked(string path)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var blocked in BlockedPathSegments)
        {
            if (normalized.Contains(blocked))
                return $"Path contains blocked sequence '{blocked}'. Path traversal is not allowed.";
        }
        return null;
    }

    private static string? CheckSensitive(string fullPath)
    {
        foreach (var sensitive in SensitiveDirectories)
        {
            if (fullPath.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase))
                return $"Access to '{sensitive}' is not allowed for security reasons.";
        }
        return null;
    }
}
