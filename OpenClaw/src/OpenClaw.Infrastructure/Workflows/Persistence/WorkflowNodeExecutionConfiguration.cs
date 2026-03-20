using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;

namespace OpenClaw.Infrastructure.Workflows.Persistence;

public class WorkflowNodeExecutionConfiguration : IEntityTypeConfiguration<WorkflowNodeExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowNodeExecution> builder)
    {
        builder.ToTable("workflow_node_executions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.WorkflowExecutionId)
            .HasColumnName("workflow_execution_id")
            .IsRequired();

        builder.Property(x => x.NodeId)
            .HasColumnName("node_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.InputJson)
            .HasColumnName("input_json")
            .HasColumnType("text");

        builder.Property(x => x.OutputJson)
            .HasColumnName("output_json")
            .HasColumnType("text");

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        // Indexes
        builder.HasIndex(x => x.WorkflowExecutionId)
            .HasDatabaseName("ix_workflow_node_executions_workflow_execution_id");

        builder.HasIndex(x => new { x.WorkflowExecutionId, x.NodeId })
            .IsUnique()
            .HasDatabaseName("uq_workflow_node_executions_execution_node");
    }
}
