using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.CronJobs.Entities;

namespace OpenClaw.Infrastructure.CronJobs.Persistence;

public class CronJobConfiguration : IEntityTypeConfiguration<CronJob>
{
    public void Configure(EntityTypeBuilder<CronJob> builder)
    {
        builder.ToTable("cron_jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ScheduleJson).HasColumnName("schedule_json").HasColumnType("text");
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.WakeMode).HasColumnName("wake_mode").IsRequired();
        builder.Property(x => x.ContextJson).HasColumnName("context_json").HasColumnType("text");
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.LastScheduledAt).HasColumnName("last_scheduled_at");

        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("ix_cron_jobs_created_by_user_id");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("ix_cron_jobs_is_active");

        builder.HasMany(x => x.Executions)
            .WithOne(x => x.CronJob)
            .HasForeignKey(x => x.CronJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
