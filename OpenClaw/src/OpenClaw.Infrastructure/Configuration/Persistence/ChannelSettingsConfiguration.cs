using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenClaw.Domain.Configuration.Entities;

namespace OpenClaw.Infrastructure.Configuration.Persistence;

public class ChannelSettingsConfiguration : IEntityTypeConfiguration<ChannelSettings>
{
    public void Configure(EntityTypeBuilder<ChannelSettings> builder)
    {
        builder.ToTable("channel_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.ChannelType)
            .HasColumnName("channel_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(x => x.EncryptedBotToken)
            .HasColumnName("encrypted_bot_token")
            .HasMaxLength(1000);

        builder.Property(x => x.WebhookUrl)
            .HasColumnName("webhook_url")
            .HasMaxLength(500);

        builder.Property(x => x.SecretToken)
            .HasColumnName("secret_token")
            .HasMaxLength(256);

        builder.Property(x => x.AllowedUserIds)
            .HasColumnName("allowed_user_ids")
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(x => new { x.UserId, x.ChannelType })
            .IsUnique()
            .HasDatabaseName("ix_channel_settings_user_id_channel_type");
    }
}
