using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Workflows.Entities;

namespace OpenClaw.Infrastructure.Workflows.Persistence;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("workflow_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.DefinitionJson)
            .HasColumnName("definition_json")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.ScheduleJson)
            .HasColumnName("schedule_json")
            .HasColumnType("text");

        builder.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.LastScheduledAt)
            .HasColumnName("last_scheduled_at");

        // Indexes
        builder.HasIndex(x => x.CreatedByUserId)
            .HasDatabaseName("ix_workflow_definitions_created_by_user_id");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("ix_workflow_definitions_is_active");

        // Relationship to executions
        builder.HasMany(x => x.Executions)
            .WithOne(x => x.WorkflowDefinition)
            .HasForeignKey(x => x.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
