using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ClawOS.Domain.Configuration.Entities;

namespace ClawOS.Infrastructure.Configuration.Persistence;

public class UserModelProviderConfiguration : IEntityTypeConfiguration<UserModelProvider>
{
    public void Configure(EntityTypeBuilder<UserModelProvider> builder)
    {
        builder.ToTable("user_model_providers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.GlobalModelProviderId)
            .HasColumnName("global_model_provider_id");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Url)
            .HasColumnName("url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ModelName)
            .HasColumnName("model_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EncryptedApiKey)
            .HasColumnName("encrypted_api_key")
            .HasMaxLength(1000);

        builder.Property(x => x.IsDefault)
            .HasColumnName("is_default")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_model_providers_user_id");

        builder.HasIndex(x => new { x.UserId, x.Name })
            .IsUnique()
            .HasDatabaseName("ix_user_model_providers_user_id_name");

        builder.HasOne<ModelProvider>()
            .WithMany()
            .HasForeignKey(x => x.GlobalModelProviderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
