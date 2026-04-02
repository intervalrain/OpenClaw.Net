using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.SkillStore.Entities;
using OpenClaw.Domain.SkillStore.Enums;
using OpenClaw.Domain.SkillStore.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.SkillStore.Persistence;

public class SkillListingRepository(AppDbContext context)
    : GenericRepository<SkillListing, Guid, AppDbContext>(context), ISkillListingRepository
{
    public async Task<SkillListing?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillListing>()
            .FirstOrDefaultAsync(x => x.Name == name, ct);
    }

    public async Task<IReadOnlyList<SkillListing>> GetAllAsync(
        SkillPublishStatus? status, string? category, string? search,
        int limit, int offset, CancellationToken ct)
    {
        var query = DbContext.Set<SkillListing>().AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(x => x.Category == category);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(x => x.Name.Contains(search) || x.DisplayName.Contains(search)
                || (x.Description != null && x.Description.Contains(search)));

        return await query
            .OrderByDescending(x => x.StarCount)
            .ThenByDescending(x => x.DownloadCount)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SkillListing>> GetByAuthorAsync(Guid authorUserId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillListing>()
            .Where(x => x.AuthorUserId == authorUserId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SkillListing>> GetPendingReviewAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<SkillListing>()
            .Where(x => x.Status == SkillPublishStatus.PendingReview)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(SkillPublishStatus? status, CancellationToken ct = default)
    {
        var query = DbContext.Set<SkillListing>().AsQueryable();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        return await query.CountAsync(ct);
    }
}

public class SkillStarRepository(AppDbContext context)
    : GenericRepository<SkillStar, Guid, AppDbContext>(context), ISkillStarRepository
{
    public async Task<SkillStar?> GetAsync(Guid skillListingId, Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillStar>()
            .FirstOrDefaultAsync(x => x.SkillListingId == skillListingId && x.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<SkillStar>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillStar>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }
}

public class SkillFollowRepository(AppDbContext context)
    : GenericRepository<SkillFollow, Guid, AppDbContext>(context), ISkillFollowRepository
{
    public async Task<SkillFollow?> GetAsync(Guid skillListingId, Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillFollow>()
            .FirstOrDefaultAsync(x => x.SkillListingId == skillListingId && x.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<SkillFollow>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillFollow>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SkillFollow>> GetBySkillAsync(Guid skillListingId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillFollow>()
            .Where(x => x.SkillListingId == skillListingId)
            .ToListAsync(ct);
    }
}

public class SkillInstallationRepository(AppDbContext context)
    : GenericRepository<SkillInstallation, Guid, AppDbContext>(context), ISkillInstallationRepository
{
    public async Task<SkillInstallation?> GetAsync(Guid skillListingId, Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillInstallation>()
            .FirstOrDefaultAsync(x => x.SkillListingId == skillListingId && x.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<SkillInstallation>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillInstallation>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.InstalledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SkillInstallation>> GetBySkillAsync(Guid skillListingId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillInstallation>()
            .Where(x => x.SkillListingId == skillListingId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SkillInstallation>> GetWithUpdatesAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillInstallation>()
            .Where(x => x.UserId == userId && x.HasUpdate)
            .ToListAsync(ct);
    }
}
