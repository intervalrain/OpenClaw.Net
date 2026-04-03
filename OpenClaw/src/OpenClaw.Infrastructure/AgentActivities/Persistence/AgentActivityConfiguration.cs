using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.AgentActivities.Entities;

namespace OpenClaw.Infrastructure.AgentActivities.Persistence;

public class AgentActivityConfiguration : IEntityTypeConfiguration<AgentActivity>
{
    public void Configure(EntityTypeBuilder<AgentActivity> builder)
    {
        builder.ToTable("agent_activities");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.SourceId).HasColumnName("source_id").HasMaxLength(256);
        builder.Property(x => x.SourceName).HasColumnName("source_name").HasMaxLength(512);
        builder.Property(x => x.Detail).HasColumnName("detail").HasMaxLength(2048);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_agent_activities_user_id");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_agent_activities_created_at");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_agent_activities_user_created");
    }
}
