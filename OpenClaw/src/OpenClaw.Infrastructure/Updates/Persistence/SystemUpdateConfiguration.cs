using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Updates.Entities;

namespace OpenClaw.Infrastructure.Updates.Persistence;

public class SystemUpdateConfiguration : IEntityTypeConfiguration<SystemUpdate>
{
    public void Configure(EntityTypeBuilder<SystemUpdate> builder)
    {
        builder.ToTable("system_updates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TagName).HasColumnName("tag_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReleaseName).HasColumnName("release_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ReleaseNotes).HasColumnName("release_notes").HasColumnType("text");
        builder.Property(x => x.HtmlUrl).HasColumnName("html_url").HasMaxLength(500);
        builder.Property(x => x.PublishedAt).HasColumnName("published_at").IsRequired();
        builder.Property(x => x.IsAcknowledged).HasColumnName("is_acknowledged").IsRequired();
        builder.Property(x => x.AcknowledgedByUserId).HasColumnName("acknowledged_by_user_id");
        builder.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(x => x.IsDismissed).HasColumnName("is_dismissed").IsRequired();
        builder.Property(x => x.DetectedAt).HasColumnName("detected_at").IsRequired();

        builder.HasIndex(x => x.TagName).IsUnique().HasDatabaseName("uq_system_updates_tag_name");
    }
}
