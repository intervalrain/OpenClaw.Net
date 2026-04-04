using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Workspaces;
using OpenClaw.Domain.Users.Repositories;
using Weda.Core.Application.Interfaces;
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
    ICurrentWorkspaceProvider currentWorkspaceProvider,
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
    private Guid GetWsId() => currentWorkspaceProvider.WorkspaceId;

    private string ResolveWsPath(string? path, bool isSuperAdmin, string scope = "my")
    {
        var root = scope == "admin" && isSuperAdmin
            ? PathSecurity.GetWorkspaceBasePath()
            : PathSecurity.GetWorkspacePath(GetWsId());

        return path is null ? root : Path.GetFullPath(Path.Combine(root, path));
    }

    /// <summary>
    /// List files and directories at the given path within the user's workspace.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> List(
        [FromQuery] string? path = null,
        [FromQuery] string scope = "my",
        [FromQuery] Guid? ownerId = null,
        CancellationToken ct = default)
    {
        path = NormalizePath(path);
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);

        // Browsing another user's public directory
        if (ownerId.HasValue && ownerId.Value != user.Id && !isSuperAdmin)
        {
            return await ListPublicDirectory(ownerId.Value, path, user.Id, ct);
        }

        var basePath = ResolveWsPath(path, isSuperAdmin, scope);
        var wsId = GetWsId();

        var error = PathSecurity.ValidateWorkspacePath(basePath, wsId, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (!Directory.Exists(basePath))
            return NotFound("Directory not found.");

        // Load all permissions for this user to resolve inheritance
        var permissions = scope == "my"
            ? await permissionRepository.GetByOwnerAsync(user.Id, ct)
            : [];

        var entries = Directory.GetFileSystemEntries(basePath)
            .Select(e =>
            {
                var isDir = Directory.Exists(e);
                var info = isDir ? null : new FileInfo(e);
                var name = Path.GetFileName(e);

                // Resolve effective visibility (inherited from parent tree)
                string? visibility = null;
                string? explicitVisibility = null;
                if (scope == "my")
                {
                    var relPath = path is null ? name : $"{path}/{name}";
                    var effective = DirectoryPermission.ResolveEffective(relPath, permissions);
                    visibility = effective.ToString();

                    // Check if this path has an explicit (non-inherited) setting
                    var exactMatch = permissions.FirstOrDefault(p => p.RelativePath == relPath.TrimStart('/').TrimEnd('/'));
                    explicitVisibility = exactMatch?.Visibility.ToString();
                }

                return new
                {
                    Name = name,
                    IsDirectory = isDir,
                    Size = info?.Length,
                    ModifiedAt = isDir
                        ? Directory.GetLastWriteTimeUtc(e)
                        : info!.LastWriteTimeUtc,
                    Visibility = visibility,
                    ExplicitVisibility = explicitVisibility
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
    /// List another user's directory — only if effective visibility is Public or PublicReadonly.
    /// </summary>
    private async Task<IActionResult> ListPublicDirectory(Guid ownerId, string? path, Guid requesterId, CancellationToken ct)
    {
        // Check visibility permission
        var ownerPermissions = await permissionRepository.GetByOwnerAsync(ownerId, ct);
        var relPath = path ?? "";
        var effective = DirectoryPermission.ResolveEffective(relPath, ownerPermissions);

        if (effective == DirectoryVisibility.Private || effective == DirectoryVisibility.Default)
            return Forbid();

        // Cross-user browsing: resolve ownerId's personal workspace
        // TODO: when shared workspaces exist, browse by workspaceId instead
        var basePath = path is null
            ? PathSecurity.GetWorkspacePath(ownerId)
            : Path.GetFullPath(Path.Combine(PathSecurity.GetWorkspacePath(ownerId), path));

        if (!Directory.Exists(basePath))
            return NotFound("Directory not found.");

        var entries = Directory.GetFileSystemEntries(basePath)
            .Select(e =>
            {
                var isDir = Directory.Exists(e);
                var info = isDir ? null : new FileInfo(e);
                var name = Path.GetFileName(e);
                var childRelPath = string.IsNullOrEmpty(relPath) ? name : $"{relPath}/{name}";
                var childEffective = DirectoryPermission.ResolveEffective(childRelPath, ownerPermissions);

                // Hide private children
                if (childEffective == DirectoryVisibility.Private)
                    return null;

                return new
                {
                    Name = name,
                    IsDirectory = isDir,
                    Size = info?.Length,
                    ModifiedAt = isDir ? Directory.GetLastWriteTimeUtc(e) : info!.LastWriteTimeUtc,
                    Visibility = childEffective.ToString(),
                    ExplicitVisibility = (string?)null
                };
            })
            .Where(e => e is not null)
            .OrderByDescending(e => e!.IsDirectory)
            .ThenBy(e => e!.Name)
            .ToList();

        return Ok(new
        {
            Path = path ?? "/",
            Entries = entries,
            OwnerId = ownerId,
            ReadOnly = effective == DirectoryVisibility.PublicReadonly
        });
    }

    /// <summary>
    /// Upload a file to the user's workspace.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> Upload(
        [FromQuery] string? path,
        [FromQuery] Guid? ownerId,
        IFormFile file,
        CancellationToken ct)
    {
        path = NormalizePath(path);
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var targetUserId = ownerId ?? user.Id;

        // Cross-user access: only Public (RW) allows writing
        if (ownerId.HasValue && ownerId.Value != user.Id && !isSuperAdmin)
        {
            var perms = await permissionRepository.GetByOwnerAsync(ownerId.Value, ct);
            var effective = DirectoryPermission.ResolveEffective(path ?? "", perms);
            if (effective != DirectoryVisibility.Public)
                return BadRequest("This directory is read-only or private.");
        }

        var wsId = ownerId.HasValue ? ownerId.Value : GetWsId();
        var targetDir = path is null
            ? PathSecurity.GetWorkspacePath(wsId)
            : Path.GetFullPath(Path.Combine(PathSecurity.GetWorkspacePath(wsId), path));

        var error = PathSecurity.ValidateWorkspacePath(targetDir, wsId, isSuperAdmin || ownerId.HasValue);
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
        error = PathSecurity.ValidateWorkspacePath(resolvedFilePath, wsId, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        await using var stream = new FileStream(resolvedFilePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return Ok(new { FileName = file.FileName, Size = file.Length });
    }

    /// <summary>
    /// Download a file. Supports ownerId for cross-user public access.
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string path, [FromQuery] Guid? ownerId = null, CancellationToken ct = default)
    {
        path = NormalizePath(path) ?? "";
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var targetUserId = ownerId ?? user.Id;

        // Cross-user access: check public visibility
        if (ownerId.HasValue && ownerId.Value != user.Id && !isSuperAdmin)
        {
            var perms = await permissionRepository.GetByOwnerAsync(ownerId.Value, ct);
            var effective = DirectoryPermission.ResolveEffective(path, perms);
            if (effective != DirectoryVisibility.Public && effective != DirectoryVisibility.PublicReadonly)
                return Forbid();
        }

        var dlWsId = ownerId.HasValue ? ownerId.Value : GetWsId();
        var filePath = Path.GetFullPath(Path.Combine(PathSecurity.GetWorkspacePath(dlWsId), path));

        if (!System.IO.File.Exists(filePath))
            return NotFound($"File not found. Path: {path}, Resolved: {filePath}");

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
        var dirPath = PathSecurity.ResolveWorkspacePath(normalizedPath, GetWsId());

        var error = PathSecurity.ValidateWorkspacePath(dirPath, GetWsId(), isSuperAdmin);
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
        var fullPath = PathSecurity.ResolveWorkspacePath(path, GetWsId());

        var error = PathSecurity.ValidateWorkspacePath(fullPath, GetWsId(), isSuperAdmin);
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

        var wsId = GetWsId();
        var sourcePath = PathSecurity.ResolveWorkspacePath(NormalizePath(request.OldPath) ?? "", wsId);
        var destPath = PathSecurity.ResolveWorkspacePath(NormalizePath(request.NewPath) ?? "", wsId);

        var error = PathSecurity.ValidateWorkspacePath(sourcePath, wsId, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        error = PathSecurity.ValidateWorkspacePath(destPath, wsId, isSuperAdmin);
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
        var workspacePath = PathSecurity.GetWorkspacePath(GetWsId());
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
        var workspacePath = PathSecurity.GetWorkspacePath(GetWsId());
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
            return BadRequest("Invalid visibility. Use: Default, Public, PublicReadonly, or Private.");

        var existing = await permissionRepository.GetAsync(user.Id, normalizedPath, ct);
        if (existing is not null)
        {
            if (visibility == DirectoryVisibility.Default)
            {
                // Default = remove explicit setting, inherit from parent
                await permissionRepository.DeleteAsync(existing, ct);
            }
            else
            {
                existing.SetVisibility(visibility);
                var uow = HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
                await uow.SaveChangesAsync(ct);
            }
        }
        else if (visibility != DirectoryVisibility.Default)
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
