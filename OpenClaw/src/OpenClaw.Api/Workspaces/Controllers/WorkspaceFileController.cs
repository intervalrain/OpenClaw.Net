using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Domain.Users.Repositories;
using OpenClaw.Domain.Workspaces.Entities;
using OpenClaw.Domain.Workspaces.Repositories;
using OpenClaw.Tools.FileSystem;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace OpenClaw.Api.Workspaces.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class WorkspaceFileController(
    ICurrentUserProvider currentUserProvider,
    IUserRepository userRepository,
    IDirectoryPermissionRepository permissionRepository,
    IConfigStore configStore) : ApiController
{
    private const long DefaultQuotaMb = 100; // 100 MB default

    /// <summary>
    /// Strip leading slashes so paths are always relative to user workspace.
    /// Frontend sends "/subfolder" but PathSecurity needs "subfolder".
    /// </summary>
    private static string? NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) || path == "/" ? null : path.TrimStart('/');

    /// <summary>
    /// Resolve the base path for a given scope and relative path.
    /// scope=my (default) → user workspace; scope=admin → workspace root (SuperAdmin only).
    /// </summary>
    private string ResolveWorkspacePath(string? path, Guid userId, bool isSuperAdmin, string scope = "my")
    {
        var root = scope == "admin" && isSuperAdmin
            ? PathSecurity.GetWorkspaceBasePath()
            : PathSecurity.GetUserWorkspacePath(userId);

        return path is null ? root : Path.GetFullPath(Path.Combine(root, path));
    }

    /// <summary>
    /// List files and directories at the given path within the user's workspace.
    /// </summary>
    [HttpGet("list")]
    public IActionResult List([FromQuery] string? path = null, [FromQuery] string scope = "my")
    {
        path = NormalizePath(path);
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var basePath = ResolveWorkspacePath(path, user.Id, isSuperAdmin, scope);

        var error = PathSecurity.ValidatePath(basePath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (!Directory.Exists(basePath))
            return NotFound("Directory not found.");

        var entries = Directory.GetFileSystemEntries(basePath)
            .Select(e =>
            {
                var isDir = Directory.Exists(e);
                var info = isDir ? null : new FileInfo(e);
                return new
                {
                    Name = Path.GetFileName(e),
                    IsDirectory = isDir,
                    Size = info?.Length,
                    ModifiedAt = isDir
                        ? Directory.GetLastWriteTimeUtc(e)
                        : info!.LastWriteTimeUtc
                };
            })
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name)
            .ToList();

        return Ok(new
        {
            Path = path ?? "/",
            Entries = entries
        });
    }

    /// <summary>
    /// Upload a file to the user's workspace.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> Upload(
        [FromQuery] string? path,
        IFormFile file,
        CancellationToken ct)
    {
        path = NormalizePath(path);
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);

        var targetDir = path is null
            ? PathSecurity.GetUserWorkspacePath(user.Id)
            : PathSecurity.ResolveUserPath(path, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(targetDir, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        // Quota check
        if (!isSuperAdmin)
        {
            var quotaError = await CheckQuotaAsync(user.Id, file.Length, ct);
            if (quotaError is not null) return BadRequest(quotaError);
        }

        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        var filePath = Path.Combine(targetDir, file.FileName);

        // Prevent overwriting outside workspace via filename manipulation
        var resolvedFilePath = Path.GetFullPath(filePath);
        error = PathSecurity.ValidatePath(resolvedFilePath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        await using var stream = new FileStream(resolvedFilePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return Ok(new { FileName = file.FileName, Size = file.Length });
    }

    /// <summary>
    /// Download a file from the user's workspace.
    /// </summary>
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        path = NormalizePath(path) ?? "";
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var filePath = PathSecurity.ResolveUserPath(path, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(filePath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found.");

        var contentType = "application/octet-stream";
        var fileName = Path.GetFileName(filePath);
        return PhysicalFile(filePath, contentType, fileName);
    }

    /// <summary>
    /// Create a directory in the user's workspace.
    /// </summary>
    [HttpPost("mkdir")]
    public IActionResult Mkdir([FromBody] MkdirRequest request)
    {
        var normalizedPath = NormalizePath(request.Path) ?? "";
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var dirPath = PathSecurity.ResolveUserPath(normalizedPath, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(dirPath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        Directory.CreateDirectory(dirPath);
        return Ok(new { Path = request.Path });
    }

    /// <summary>
    /// Delete a file or empty directory from the user's workspace.
    /// </summary>
    [HttpDelete]
    public IActionResult Delete([FromQuery] string path)
    {
        path = NormalizePath(path) ?? "";
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var fullPath = PathSecurity.ResolveUserPath(path, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(fullPath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
            return Ok(new { Deleted = path });
        }

        if (Directory.Exists(fullPath))
        {
            if (Directory.GetFileSystemEntries(fullPath).Length > 0)
                return BadRequest("Directory is not empty.");

            Directory.Delete(fullPath);
            return Ok(new { Deleted = path });
        }

        return NotFound("File or directory not found.");
    }

    /// <summary>
    /// Rename/move a file or directory within the user's workspace.
    /// </summary>
    [HttpPut("rename")]
    public IActionResult Rename([FromBody] RenameRequest request)
    {
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);

        var sourcePath = PathSecurity.ResolveUserPath(NormalizePath(request.OldPath) ?? "", user.Id, isSuperAdmin);
        var destPath = PathSecurity.ResolveUserPath(NormalizePath(request.NewPath) ?? "", user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(sourcePath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        error = PathSecurity.ValidatePath(destPath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (System.IO.File.Exists(sourcePath))
        {
            System.IO.File.Move(sourcePath, destPath);
            return Ok(new { From = request.OldPath, To = request.NewPath });
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destPath);
            return Ok(new { From = request.OldPath, To = request.NewPath });
        }

        return NotFound("Source not found.");
    }

    /// <summary>
    /// Get workspace usage and quota for the current user.
    /// </summary>
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var workspacePath = PathSecurity.GetUserWorkspacePath(user.Id);
        var usedBytes = GetDirectorySize(workspacePath);

        if (isSuperAdmin)
        {
            return Ok(new
            {
                UsedBytes = usedBytes,
                UsedMb = Math.Round((double)usedBytes / (1024 * 1024), 2),
                QuotaMb = -1, // unlimited
                UsagePercent = 0,
                Unlimited = true
            });
        }

        var quotaMb = await GetQuotaMbAsync(user.Id, ct);
        return Ok(new
        {
            UsedBytes = usedBytes,
            UsedMb = Math.Round((double)usedBytes / (1024 * 1024), 2),
            QuotaMb = quotaMb,
            UsagePercent = quotaMb > 0 ? Math.Round((double)usedBytes / (quotaMb * 1024 * 1024) * 100, 1) : 0,
            Unlimited = false
        });
    }

    private async Task<string?> CheckQuotaAsync(Guid userId, long additionalBytes, CancellationToken ct)
    {
        var workspacePath = PathSecurity.GetUserWorkspacePath(userId);
        var currentUsage = GetDirectorySize(workspacePath);
        var quotaMb = await GetQuotaMbAsync(userId, ct);
        var quotaBytes = quotaMb * 1024 * 1024;

        if (currentUsage + additionalBytes > quotaBytes)
        {
            var usedMb = Math.Round((double)currentUsage / (1024 * 1024), 1);
            return $"Workspace quota exceeded. Used: {usedMb} MB / {quotaMb} MB. Contact admin to increase quota.";
        }

        return null;
    }

    private async Task<long> GetQuotaMbAsync(Guid userId, CancellationToken ct)
    {
        // Per-user override first
        var dbUser = await userRepository.GetByIdAsync(userId, ct);
        if (dbUser?.WorkspaceQuotaMb is not null)
            return dbUser.WorkspaceQuotaMb.Value;

        // System default from app-config
        var configValue = configStore.Get("WORKSPACE_QUOTA_MB");
        if (long.TryParse(configValue, out var configQuota))
            return configQuota;

        return DefaultQuotaMb;
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return new DirectoryInfo(path)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }

    // ===== Directory Permissions =====

    /// <summary>
    /// Get all directory permissions for the current user's workspace.
    /// </summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var perms = await permissionRepository.GetByOwnerAsync(user.Id, ct);
        return Ok(perms.Select(p => new
        {
            p.RelativePath,
            Visibility = p.Visibility.ToString(),
            p.CreatedAt
        }));
    }

    /// <summary>
    /// Set visibility for a directory in the current user's workspace.
    /// </summary>
    [HttpPut("permissions")]
    public async Task<IActionResult> SetPermission([FromBody] SetPermissionRequest request, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var normalizedPath = NormalizePath(request.Path) ?? "";

        if (!Enum.TryParse<DirectoryVisibility>(request.Visibility, true, out var visibility))
            return BadRequest("Invalid visibility. Use: Private, PublicReadonly, or Public.");

        // Verify directory exists
        var fullPath = PathSecurity.ResolveUserPath(normalizedPath, user.Id);
        if (!Directory.Exists(fullPath))
            return BadRequest("Directory does not exist.");

        var existing = await permissionRepository.GetAsync(user.Id, normalizedPath, ct);
        if (existing is not null)
        {
            if (visibility == DirectoryVisibility.Private)
            {
                await permissionRepository.DeleteAsync(existing, ct);
            }
            else
            {
                existing.SetVisibility(visibility);
                await permissionRepository.AddAsync(existing, ct);
            }
        }
        else if (visibility != DirectoryVisibility.Private)
        {
            var perm = DirectoryPermission.Create(user.Id, normalizedPath, visibility);
            await permissionRepository.AddAsync(perm, ct);
        }

        return Ok(new { Path = normalizedPath, Visibility = visibility.ToString() });
    }

    /// <summary>
    /// Browse public directories from all users.
    /// </summary>
    [HttpGet("public")]
    public async Task<IActionResult> GetPublicDirectories(CancellationToken ct)
    {
        var perms = await permissionRepository.GetPublicDirectoriesAsync(ct);
        var userRepo = userRepository;

        var result = new List<object>();
        foreach (var perm in perms)
        {
            var owner = await userRepo.GetByIdAsync(perm.OwnerUserId, ct);
            result.Add(new
            {
                OwnerUserId = perm.OwnerUserId,
                OwnerName = owner?.Name ?? "Unknown",
                perm.RelativePath,
                Visibility = perm.Visibility.ToString()
            });
        }

        return Ok(result);
    }
}

public record MkdirRequest(string Path);
public record RenameRequest(string OldPath, string NewPath);
public record SetPermissionRequest(string Path, string Visibility);
