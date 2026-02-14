using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.ValueObjects;

namespace OpenClaw.Infrastructure.Users.Persistence;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasConversion(
                email => email.Value,
                value => UserEmail.Create(value).Value)
            .HasMaxLength(UserEmail.MaxLength)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasConversion(
                hash => hash.Value,
                value => PasswordHash.Create(value))
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(u => u.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(UserStatus.Active);

        // ValueComparer for List<string> collections
        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        // Store roles as comma-separated string
        builder.Property<List<string>>("_roles")
            .HasColumnName("Roles")
            .HasConversion(
                roles => string.Join(",", roles),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property<List<string>>("_roles")
            .HasMaxLength(500)
            .IsRequired();

        // Store permissions as comma-separated string
        builder.Property<List<string>>("_permissions")
            .HasColumnName("Permissions")
            .HasConversion(
                perms => string.Join(",", perms),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property<List<string>>("_permissions")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired(false);

        builder.Property(u => u.LastLoginAt)
            .IsRequired(false);
    }
}
