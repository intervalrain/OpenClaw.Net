using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Notifications.Entities;

namespace OpenClaw.Infrastructure.Notifications.Persistence;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Message).HasColumnName("message").HasColumnType("text");
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id").HasMaxLength(255);
        builder.Property(x => x.IsRead).HasColumnName("is_read").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ReadAt).HasColumnName("read_at");

        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_notifications_user_id");
        builder.HasIndex(x => new { x.UserId, x.IsRead }).HasDatabaseName("ix_notifications_user_id_is_read");
    }
}
