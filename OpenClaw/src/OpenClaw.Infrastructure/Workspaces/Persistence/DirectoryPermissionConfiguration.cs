using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Workspaces.Entities;

namespace OpenClaw.Infrastructure.Workspaces.Persistence;

public class DirectoryPermissionConfiguration : IEntityTypeConfiguration<DirectoryPermission>
{
    public void Configure(EntityTypeBuilder<DirectoryPermission> builder)
    {
        builder.ToTable("directory_permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(x => x.RelativePath).HasColumnName("relative_path").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.OwnerUserId, x.RelativePath }).IsUnique();
        builder.HasIndex(x => x.Visibility);
    }
}
