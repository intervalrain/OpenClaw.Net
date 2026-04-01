using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClawOS.Domain.CronJobs.Entities;

namespace ClawOS.Infrastructure.CronJobs.Persistence;

public class CronJobExecutionConfiguration : IEntityTypeConfiguration<CronJobExecution>
{
    public void Configure(EntityTypeBuilder<CronJobExecution> builder)
    {
        builder.ToTable("cron_job_executions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CronJobId).HasColumnName("cron_job_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.Trigger).HasColumnName("trigger").IsRequired();
        builder.Property(x => x.OutputText).HasColumnName("output_text").HasColumnType("text");
        builder.Property(x => x.ToolCallsJson).HasColumnName("tool_calls_json").HasColumnType("text");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(x => x.CronJobId).HasDatabaseName("ix_cron_job_executions_cron_job_id");
    }
}
