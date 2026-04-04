using Microsoft.AspNetCore.Http;
using OpenClaw.Contracts.Workspaces;
using OpenClaw.Domain.Workspaces.Repositories;
using Weda.Core.Application.Security;

namespace OpenClaw.Infrastructure.Workspaces;

public class CurrentWorkspaceProvider(
    IHttpContextAccessor httpContextAccessor,
    IWorkspaceRepository workspaceRepository,
    ICurrentUserProvider currentUserProvider) : ICurrentWorkspaceProvider
{
    private Guid? _cached;

    public Guid WorkspaceId => _cached ??= ResolveWorkspaceId();

    private Guid ResolveWorkspaceId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return Guid.Empty;

        if (httpContext.Request.Headers.TryGetValue("X-Workspace-Id", out var headerValue)
            && Guid.TryParse(headerValue.FirstOrDefault(), out var workspaceId))
        {
            return workspaceId;
        }

        try
        {
            var userId = currentUserProvider.GetCurrentUser().Id;
            var personal = workspaceRepository
                .GetPersonalAsync(userId)
                .GetAwaiter().GetResult();
            return personal?.Id ?? Guid.Empty;
        }
        catch
        {
            return Guid.Empty;
        }
    }
}
