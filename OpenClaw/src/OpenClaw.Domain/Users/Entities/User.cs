using ErrorOr;

using Weda.Core.Application.Security.Models;

using Weda.Core.Domain;
using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.ValueObjects;

namespace OpenClaw.Domain.Users.Entities;

public class User : AggregateRoot<Guid>
{
    public UserEmail Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public UserStatus Status { get; private set; }

    private readonly List<string> _roles = [];
    public IReadOnlyList<string> Roles => _roles.AsReadOnly();

    private readonly List<string> _permissions = [];
    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public static ErrorOr<User> Create(
        string email,
        string passwordHash,
        string name,
        List<string>? roles = null,
        List<string>? permissions = null)
    {
        var emailResult = UserEmail.Create(email);
        if (emailResult.IsError)
        {
            return emailResult.Errors;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return UserErrors.EmptyName;
        }

        if (name.Length > 100)
        {
            return UserErrors.NameTooLong;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = emailResult.Value,
            PasswordHash = PasswordHash.Create(passwordHash),
            Name = name.Trim(),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        user._roles.AddRange(roles ?? [Role.User]);
        user._permissions.AddRange(permissions ?? []);

        return user;
    }

    public ErrorOr<Success> UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return UserErrors.EmptyName;
        }

        if (newName.Length > 100)
        {
            return UserErrors.NameTooLong;
        }

        Name = newName.Trim();
        UpdatedAt = DateTime.UtcNow;
        return Result.Success;
    }

    public ErrorOr<Success> UpdateEmail(string newEmail)
    {
        var emailResult = UserEmail.Create(newEmail);
        if (emailResult.IsError)
        {
            return emailResult.Errors;
        }

        Email = emailResult.Value;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = PasswordHash.Create(newPasswordHash);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(UserStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public ErrorOr<Success> UpdateRoles(List<string> newRoles, Guid currentUserId, IReadOnlyList<string> currentUserRoles)
    {
        if (!currentUserRoles.Contains(Role.SuperAdmin))
        {
            return UserErrors.OnlySuperAdminCanChangeRoles;
        }

        // Prevent removing own SuperAdmin role
        if (Id == currentUserId &&
            _roles.Contains(Role.SuperAdmin) &&
            !newRoles.Contains(Role.SuperAdmin))
        {
            return UserErrors.CannotRemoveOwnSuperAdmin;
        }

        _roles.Clear();
        _roles.AddRange(newRoles);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success;
    }

    public ErrorOr<Success> UpdatePermissions(List<string> newPermissions, IReadOnlyList<string> currentUserRoles)
    {
        if (!currentUserRoles.Contains(Role.SuperAdmin))
        {
            return UserErrors.OnlySuperAdminCanChangePermissions;
        }

        _permissions.Clear();
        _permissions.AddRange(newPermissions);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    private User()
    {
    }
}
