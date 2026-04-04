using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.HierarchicalAgents.Entities;

namespace OpenClaw.Infrastructure.HierarchicalAgents.Persistence;

public class AgentExecutionConfiguration : IEntityTypeConfiguration<AgentExecution>
{
    public void Configure(EntityTypeBuilder<AgentExecution> builder)
    {
        builder.ToTable("agent_executions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ParentExecutionId).HasColumnName("parent_execution_id");
        builder.Property(x => x.AgentName).HasColumnName("agent_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.TaskGraphJson).HasColumnName("task_graph_json").HasColumnType("text");
        builder.Property(x => x.NodeStatesJson).HasColumnName("node_states_json").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.TotalTokensUsed).HasColumnName("total_tokens_used").HasPrecision(18, 4);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("text");

        builder.HasIndex(x => x.ParentExecutionId).HasDatabaseName("ix_agent_executions_parent_id");
        builder.HasIndex(x => x.AgentName).HasDatabaseName("ix_agent_executions_agent_name");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_agent_executions_status");
    }
}
