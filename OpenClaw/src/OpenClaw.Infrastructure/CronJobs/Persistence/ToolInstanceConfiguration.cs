using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.CronJobs.Entities;

namespace OpenClaw.Infrastructure.CronJobs.Persistence;

public class ToolInstanceConfiguration : IEntityTypeConfiguration<ToolInstance>
{
    public void Configure(EntityTypeBuilder<ToolInstance> builder)
    {
        builder.ToTable("tool_instances");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ToolName).HasColumnName("tool_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ArgsJson).HasColumnName("args_json").HasColumnType("text");
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("ix_tool_instances_created_by_user_id");
        builder.HasIndex(x => new { x.Name, x.CreatedByUserId })
            .IsUnique()
            .HasDatabaseName("ix_tool_instances_name_user_id");
    }
}
