using OpenClaw.Domain.SkillStore.Enums;
using OpenClaw.Domain.SkillStore.Events;
using Weda.Core.Domain;

namespace OpenClaw.Domain.SkillStore.Entities;

/// <summary>
/// Represents a skill published by an enterprise user to the internal skill store.
/// Requires admin review before becoming available to other users.
/// </summary>
public class SkillListing : AggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Version { get; private set; } = null!;
    public string? IconUrl { get; private set; }
    public string? RepositoryUrl { get; private set; }
    public string? Category { get; private set; }
    public string? Tags { get; private set; }
    public string ContentJson { get; private set; } = null!;
    public SkillPublishStatus Status { get; private set; }
    public string? ReviewComment { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string AuthorName { get; private set; } = null!;
    public int StarCount { get; private set; }
    public int FollowerCount { get; private set; }
    public int DownloadCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<SkillReview> _reviews = [];
    public IReadOnlyCollection<SkillReview> Reviews => _reviews.AsReadOnly();

    private readonly List<SkillStar> _stars = [];
    public IReadOnlyCollection<SkillStar> Stars => _stars.AsReadOnly();

    private readonly List<SkillFollow> _follows = [];
    public IReadOnlyCollection<SkillFollow> Follows => _follows.AsReadOnly();

    private readonly List<SkillInstallation> _installations = [];
    public IReadOnlyCollection<SkillInstallation> Installations => _installations.AsReadOnly();

    private SkillListing() : base(Guid.NewGuid()) { }

    public static SkillListing Create(
        string name,
        string displayName,
        string? description,
        string version,
        string contentJson,
        Guid authorUserId,
        string authorName,
        string? iconUrl = null,
        string? repositoryUrl = null,
        string? category = null,
        string? tags = null)
    {
        var listing = new SkillListing
        {
            Name = name.ToLowerInvariant().Trim(),
            DisplayName = displayName.Trim(),
            Description = description,
            Version = version,
            ContentJson = contentJson,
            AuthorUserId = authorUserId,
            AuthorName = authorName,
            IconUrl = iconUrl,
            RepositoryUrl = repositoryUrl,
            Category = category,
            Tags = tags,
            Status = SkillPublishStatus.PendingReview,
            CreatedAt = DateTime.UtcNow,
        };

        return listing;
    }

    public void Update(
        string? displayName = null,
        string? description = null,
        string? iconUrl = null,
        string? repositoryUrl = null,
        string? category = null,
        string? tags = null)
    {
        if (displayName != null) DisplayName = displayName.Trim();
        if (description != null) Description = description;
        if (iconUrl != null) IconUrl = iconUrl;
        if (repositoryUrl != null) RepositoryUrl = repositoryUrl;
        if (category != null) Category = category;
        if (tags != null) Tags = tags;
        UpdatedAt = DateTime.UtcNow;
    }

    public void PublishNewVersion(string newVersion, string contentJson)
    {
        var oldVersion = Version;
        Version = newVersion;
        ContentJson = contentJson;
        Status = SkillPublishStatus.PendingReview;
        ReviewComment = null;
        ReviewedByUserId = null;
        ReviewedAt = null;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SkillVersionUpdatedEvent(Id, Name, oldVersion, newVersion));
    }

    public void Approve(Guid reviewerUserId, string? comment = null)
    {
        Status = SkillPublishStatus.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = DateTime.UtcNow;
        ReviewComment = comment;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SkillApprovedEvent(Id, Name, reviewerUserId));
    }

    public void Reject(Guid reviewerUserId, string? reason = null)
    {
        Status = SkillPublishStatus.Rejected;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = DateTime.UtcNow;
        ReviewComment = reason;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SkillRejectedEvent(Id, Name, reviewerUserId, reason));
    }

    public void Deprecate()
    {
        Status = SkillPublishStatus.Deprecated;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementStarCount() => StarCount++;
    public void DecrementStarCount() { if (StarCount > 0) StarCount--; }
    public void IncrementFollowerCount() => FollowerCount++;
    public void DecrementFollowerCount() { if (FollowerCount > 0) FollowerCount--; }
    public void IncrementDownloadCount() => DownloadCount++;
}
