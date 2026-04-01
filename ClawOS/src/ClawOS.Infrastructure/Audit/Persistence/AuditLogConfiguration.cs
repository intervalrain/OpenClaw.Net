using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClawOS.Domain.Audit.Entities;

namespace ClawOS.Infrastructure.Audit.Persistence;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.UserEmail).HasColumnName("user_email").HasMaxLength(256);
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Path).HasColumnName("path").HasMaxLength(500).IsRequired();
        builder.Property(x => x.StatusCode).HasColumnName("status_code");
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(200);
        builder.Property(x => x.Timestamp).HasColumnName("timestamp").IsRequired();

        // Indexes for common query patterns
        builder.HasIndex(x => x.Timestamp).IsDescending();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Action);
    }
}
