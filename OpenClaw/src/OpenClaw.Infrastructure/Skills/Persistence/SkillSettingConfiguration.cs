using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenClaw.Domain.Skills.Entities;

namespace OpenClaw.Infrastructure.Skills.Persistence;

public class SkillSettingConfiguration : IEntityTypeConfiguration<SkillSetting>
{
    public void Configure(EntityTypeBuilder<SkillSetting> builder)
    {
        builder.ToTable("skill_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.SkillName)
            .HasColumnName("skill_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.HasIndex(x => x.SkillName)
            .IsUnique()
            .HasDatabaseName("uq_skill_settings_skill_name");
    }
}