using Weda.Core.Domain;

namespace OpenClaw.Domain.Workspaces.Entities;

public class Workspace : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public bool IsPersonal { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<WorkspaceMember> _members = [];
    public IReadOnlyList<WorkspaceMember> Members => _members.AsReadOnly();

    private Workspace() : base(Guid.NewGuid()) { }

    public static Workspace CreatePersonal(Guid userId, string userName)
    {
        var ws = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = $"{userName}'s Workspace",
            OwnerUserId = userId,
            IsPersonal = true,
            CreatedAt = DateTime.UtcNow
        };
        ws._members.Add(WorkspaceMember.Create(ws.Id, userId, WorkspaceRole.Owner));
        return ws;
    }

    public static Workspace CreateShared(Guid ownerUserId, string name, string? description = null)
    {
        var ws = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            OwnerUserId = ownerUserId,
            IsPersonal = false,
            CreatedAt = DateTime.UtcNow
        };
        ws._members.Add(WorkspaceMember.Create(ws.Id, ownerUserId, WorkspaceRole.Owner));
        return ws;
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public WorkspaceMember AddMember(Guid userId, WorkspaceRole role = WorkspaceRole.Member)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("User is already a member of this workspace");

        var member = WorkspaceMember.Create(Id, userId, role);
        _members.Add(member);
        UpdatedAt = DateTime.UtcNow;
        return member;
    }

    public void RemoveMember(Guid userId)
    {
        if (userId == OwnerUserId)
            throw new InvalidOperationException("Cannot remove the workspace owner");

        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new InvalidOperationException("User is not a member of this workspace");

        _members.Remove(member);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsMember(Guid userId) => _members.Any(m => m.UserId == userId);
    public WorkspaceRole? GetMemberRole(Guid userId) => _members.FirstOrDefault(m => m.UserId == userId)?.Role;
}
