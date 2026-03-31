using Weda.Core.Domain;

namespace OpenClaw.Domain.Workspaces.Entities;

public class WorkspaceMember : Entity<Guid>
{
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private WorkspaceMember() : base(Guid.NewGuid()) { }

    public static WorkspaceMember Create(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        return new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
    }

    public void UpdateRole(WorkspaceRole role) => Role = role;
}

public enum WorkspaceRole
{
    Viewer = 0,
    Member = 1,
    Owner = 2
}
