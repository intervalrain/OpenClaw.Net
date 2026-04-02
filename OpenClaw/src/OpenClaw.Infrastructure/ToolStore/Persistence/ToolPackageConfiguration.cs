using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.ToolStore.Entities;

namespace OpenClaw.Infrastructure.ToolStore.Persistence;

public class ToolPackageConfiguration : IEntityTypeConfiguration<ToolPackage>
{
    public void Configure(EntityTypeBuilder<ToolPackage> builder)
    {
        builder.ToTable("tool_packages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.PackageId).HasColumnName("package_id").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.Author).HasColumnName("author").HasMaxLength(255);
        builder.Property(x => x.CurrentVersion).HasColumnName("current_version").HasMaxLength(50).IsRequired();
        builder.Property(x => x.InstalledVersion).HasColumnName("installed_version").HasMaxLength(50);
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.IconUrl).HasColumnName("icon_url").HasMaxLength(500);
        builder.Property(x => x.RepositoryUrl).HasColumnName("repository_url").HasMaxLength(500);
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(x => x.InstalledAt).HasColumnName("installed_at");
        builder.Property(x => x.InstalledByUserId).HasColumnName("installed_by_user_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.PackageId).IsUnique().HasDatabaseName("uq_tool_packages_package_id");
    }
}
