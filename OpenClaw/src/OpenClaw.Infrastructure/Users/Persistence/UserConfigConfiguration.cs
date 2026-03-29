using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Users.Entities;

namespace OpenClaw.Infrastructure.Users.Persistence;

public class UserConfigConfiguration : IEntityTypeConfiguration<UserConfig>
{
    public void Configure(EntityTypeBuilder<UserConfig> builder)
    {
        builder.ToTable("user_configs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasColumnType("text");
        builder.Property(x => x.IsSecret).HasColumnName("is_secret").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.UserId, x.Key })
            .IsUnique()
            .HasDatabaseName("ix_user_configs_user_id_key");
    }
}
