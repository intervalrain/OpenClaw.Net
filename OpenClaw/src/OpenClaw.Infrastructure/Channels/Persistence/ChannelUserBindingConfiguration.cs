using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Channels.Entities;

namespace OpenClaw.Infrastructure.Channels.Persistence;

public class ChannelUserBindingConfiguration : IEntityTypeConfiguration<ChannelUserBinding>
{
    public void Configure(EntityTypeBuilder<ChannelUserBinding> builder)
    {
        builder.ToTable("channel_user_bindings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ExternalUserId).HasColumnName("external_user_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.OpenClawUserId).HasColumnName("openclaw_user_id").IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        // One binding per platform+externalUser
        builder.HasIndex(x => new { x.Platform, x.ExternalUserId }).IsUnique();
        builder.HasIndex(x => x.OpenClawUserId);
    }
}
