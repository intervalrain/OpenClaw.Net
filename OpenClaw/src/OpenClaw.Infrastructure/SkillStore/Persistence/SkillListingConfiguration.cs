using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClaw.Domain.SkillStore.Entities;

namespace OpenClaw.Infrastructure.SkillStore.Persistence;

public class SkillListingConfiguration : IEntityTypeConfiguration<SkillListing>
{
    public void Configure(EntityTypeBuilder<SkillListing> builder)
    {
        builder.ToTable("skill_listings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.Version).HasColumnName("version").HasMaxLength(50).IsRequired();
        builder.Property(x => x.IconUrl).HasColumnName("icon_url").HasMaxLength(500);
        builder.Property(x => x.RepositoryUrl).HasColumnName("repository_url").HasMaxLength(500);
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(x => x.Tags).HasColumnName("tags").HasMaxLength(500);
        builder.Property(x => x.ContentJson).HasColumnName("content_json").HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasColumnType("text");
        builder.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.AuthorUserId).HasColumnName("author_user_id").IsRequired();
        builder.Property(x => x.AuthorName).HasColumnName("author_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.StarCount).HasColumnName("star_count").IsRequired();
        builder.Property(x => x.FollowerCount).HasColumnName("follower_count").IsRequired();
        builder.Property(x => x.DownloadCount).HasColumnName("download_count").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_skill_listings_name");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_skill_listings_status");
        builder.HasIndex(x => x.AuthorUserId).HasDatabaseName("ix_skill_listings_author_user_id");
        builder.HasIndex(x => x.Category).HasDatabaseName("ix_skill_listings_category");

        builder.HasMany(x => x.Reviews)
            .WithOne()
            .HasForeignKey(x => x.SkillListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Stars)
            .WithOne()
            .HasForeignKey(x => x.SkillListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Follows)
            .WithOne()
            .HasForeignKey(x => x.SkillListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Installations)
            .WithOne()
            .HasForeignKey(x => x.SkillListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SkillReviewConfiguration : IEntityTypeConfiguration<SkillReview>
{
    public void Configure(EntityTypeBuilder<SkillReview> builder)
    {
        builder.ToTable("skill_reviews");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SkillListingId).HasColumnName("skill_listing_id").IsRequired();
        builder.Property(x => x.ReviewerUserId).HasColumnName("reviewer_user_id").IsRequired();
        builder.Property(x => x.ReviewerName).HasColumnName("reviewer_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Decision).HasColumnName("decision").IsRequired();
        builder.Property(x => x.Comment).HasColumnName("comment").HasColumnType("text");
        builder.Property(x => x.VersionReviewed).HasColumnName("version_reviewed").HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.SkillListingId).HasDatabaseName("ix_skill_reviews_skill_listing_id");
    }
}

public class SkillStarConfiguration : IEntityTypeConfiguration<SkillStar>
{
    public void Configure(EntityTypeBuilder<SkillStar> builder)
    {
        builder.ToTable("skill_stars");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SkillListingId).HasColumnName("skill_listing_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.SkillListingId, x.UserId }).IsUnique()
            .HasDatabaseName("uq_skill_stars_listing_user");
    }
}

public class SkillFollowConfiguration : IEntityTypeConfiguration<SkillFollow>
{
    public void Configure(EntityTypeBuilder<SkillFollow> builder)
    {
        builder.ToTable("skill_follows");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SkillListingId).HasColumnName("skill_listing_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.SkillListingId, x.UserId }).IsUnique()
            .HasDatabaseName("uq_skill_follows_listing_user");
    }
}

public class SkillInstallationConfiguration : IEntityTypeConfiguration<SkillInstallation>
{
    public void Configure(EntityTypeBuilder<SkillInstallation> builder)
    {
        builder.ToTable("skill_installations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SkillListingId).HasColumnName("skill_listing_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.InstalledVersion).HasColumnName("installed_version").HasMaxLength(50).IsRequired();
        builder.Property(x => x.HasUpdate).HasColumnName("has_update").IsRequired();
        builder.Property(x => x.InstalledAt).HasColumnName("installed_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.SkillListingId, x.UserId }).IsUnique()
            .HasDatabaseName("uq_skill_installations_listing_user");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_skill_installations_user_id");
    }
}
