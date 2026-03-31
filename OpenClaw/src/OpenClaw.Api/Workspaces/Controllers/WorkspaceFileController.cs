using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Domain.Users.Repositories;
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
    IConfigStore configStore) : ApiController
{
    private const long DefaultQuotaMb = 100; // 100 MB default
    /// <summary>
    /// List files and directories at the given path within the user's workspace.
    /// </summary>
    [HttpGet("list")]
    public IActionResult List([FromQuery] string? path = null)
    {
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var basePath = path is null
            ? PathSecurity.GetUserWorkspacePath(user.Id)
            : PathSecurity.ResolveUserPath(path, user.Id, isSuperAdmin);

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
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);

        var targetDir = path is null
            ? PathSecurity.GetUserWorkspacePath(user.Id)
            : PathSecurity.ResolveUserPath(path, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(targetDir, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (!isSuperAdmin && PathSecurity.IsSharedPath(targetDir))
            return BadRequest("Cannot upload to shared workspace.");

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
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var dirPath = PathSecurity.ResolveUserPath(request.Path, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(dirPath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (!isSuperAdmin && PathSecurity.IsSharedPath(dirPath))
            return BadRequest("Cannot create directories in shared workspace.");

        Directory.CreateDirectory(dirPath);
        return Ok(new { Path = request.Path });
    }

    /// <summary>
    /// Delete a file or empty directory from the user's workspace.
    /// </summary>
    [HttpDelete]
    public IActionResult Delete([FromQuery] string path)
    {
        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);
        var fullPath = PathSecurity.ResolveUserPath(path, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(fullPath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (!isSuperAdmin && PathSecurity.IsSharedPath(fullPath))
            return BadRequest("Cannot delete from shared workspace.");

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

        var sourcePath = PathSecurity.ResolveUserPath(request.OldPath, user.Id, isSuperAdmin);
        var destPath = PathSecurity.ResolveUserPath(request.NewPath, user.Id, isSuperAdmin);

        var error = PathSecurity.ValidatePath(sourcePath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        error = PathSecurity.ValidatePath(destPath, user.Id, isSuperAdmin);
        if (error is not null) return BadRequest(error);

        if (!isSuperAdmin && (PathSecurity.IsSharedPath(sourcePath) || PathSecurity.IsSharedPath(destPath)))
            return BadRequest("Cannot modify shared workspace.");

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
        var workspacePath = PathSecurity.GetUserWorkspacePath(user.Id);
        var usedBytes = GetDirectorySize(workspacePath);
        var quotaMb = await GetQuotaMbAsync(user.Id, ct);

        return Ok(new
        {
            UsedBytes = usedBytes,
            UsedMb = Math.Round((double)usedBytes / (1024 * 1024), 2),
            QuotaMb = quotaMb,
            UsagePercent = quotaMb > 0 ? Math.Round((double)usedBytes / (quotaMb * 1024 * 1024) * 100, 1) : 0
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
}

public record MkdirRequest(string Path);
public record RenameRequest(string OldPath, string NewPath);
