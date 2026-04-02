using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Notifications.Entities;

namespace OpenClaw.Infrastructure.Notifications.Persistence;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.Type).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(500);

        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}
