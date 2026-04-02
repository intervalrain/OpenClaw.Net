using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenClaw.Domain.Auth.Entities;

namespace OpenClaw.Infrastructure.Auth.Persistence;

public class EmailVerificationConfiguration : IEntityTypeConfiguration<EmailVerification>
{
    public void Configure(EntityTypeBuilder<EmailVerification> builder)
    {
        builder.ToTable("email_verification");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(6).IsRequired();

        builder.HasIndex(e => e.Email);
    }
}