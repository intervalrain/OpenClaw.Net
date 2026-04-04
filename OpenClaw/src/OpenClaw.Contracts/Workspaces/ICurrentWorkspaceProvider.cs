namespace OpenClaw.Contracts.Workspaces;

public interface ICurrentWorkspaceProvider
{
    Guid WorkspaceId { get; }
}
