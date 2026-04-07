using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.Agents.Entities;

namespace OpenClaw.Infrastructure.Agents.Persistence;

public class AgentDefinitionConfiguration : IEntityTypeConfiguration<AgentDefinition>
{
    public void Configure(EntityTypeBuilder<AgentDefinition> builder)
    {
        builder.ToTable("agent_definitions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.SystemPrompt).HasColumnName("system_prompt").HasColumnType("text").IsRequired();
        builder.Property(x => x.ToolsJson).HasColumnName("tools_json").HasColumnType("text").IsRequired();
        builder.Property(x => x.SubAgentIdsJson).HasColumnName("sub_agent_ids_json").HasColumnType("text").IsRequired();
        builder.Property(x => x.MaxIterations).HasColumnName("max_iterations").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("ix_agent_definitions_user_id");
        builder.HasIndex(x => new { x.WorkspaceId, x.Name }).IsUnique().HasDatabaseName("ix_agent_definitions_ws_name");
    }
}