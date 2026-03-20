using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;

namespace OpenClaw.Infrastructure.Workflows.Persistence;

public class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowExecution> builder)
    {
        builder.ToTable("workflow_executions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.WorkflowDefinitionId)
            .HasColumnName("workflow_definition_id")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Trigger)
            .HasColumnName("trigger")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.InputJson)
            .HasColumnName("input_json")
            .HasColumnType("text");

        builder.Property(x => x.OutputJson)
            .HasColumnName("output_json")
            .HasColumnType("text");

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(x => x.PendingApprovalNodeId)
            .HasColumnName("pending_approval_node_id")
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(x => x.WorkflowDefinitionId)
            .HasDatabaseName("ix_workflow_executions_workflow_definition_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_workflow_executions_status");

        builder.HasIndex(x => x.StartedAt)
            .HasDatabaseName("ix_workflow_executions_started_at");

        // Relationship to node executions
        builder.HasMany(x => x.NodeExecutions)
            .WithOne(x => x.WorkflowExecution)
            .HasForeignKey(x => x.WorkflowExecutionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
