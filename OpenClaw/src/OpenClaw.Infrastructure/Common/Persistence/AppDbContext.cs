using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.Users.Entities;
using Weda.Core.Application.Security.Models;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Common.Persistence;

public class AppDbContext : WedaDbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IHttpContextAccessor httpContextAccessor,
        IPublisher publisher) : base(options, httpContextAccessor, publisher)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Current user ID resolved from JWT claims. Returns Guid.Empty only when no HTTP context
    /// (e.g. background services). Never used as a "bypass" — see IsSuperAdmin for admin access.
    /// </summary>
    private Guid CurrentUserId =>
        Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirstValue("id"), out var id)
            ? id
            : Guid.Empty;

    /// <summary>
    /// Whether the current user has SuperAdmin role. SuperAdmin bypasses query filters
    /// to see all users' data. Background services (no HTTP context) also bypass.
    /// </summary>
    private bool IsSuperAdmin =>
        _httpContextAccessor.HttpContext is null ||
        (_httpContextAccessor.HttpContext.User?.IsInRole(Role.SuperAdmin) ?? false);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        // Global query filters for multi-tenant isolation.
        // SuperAdmin and background services bypass filters.
        // Regular users only see their own data.
        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(e => IsSuperAdmin || e.UserId == CurrentUserId);

        modelBuilder.Entity<CronJob>()
            .HasQueryFilter(e => IsSuperAdmin || e.CreatedByUserId == CurrentUserId);

        modelBuilder.Entity<CronJobExecution>()
            .HasQueryFilter(e => IsSuperAdmin || e.UserId == CurrentUserId);

        modelBuilder.Entity<ToolInstance>()
            .HasQueryFilter(e => IsSuperAdmin || e.CreatedByUserId == CurrentUserId);

        modelBuilder.Entity<UserModelProvider>()
            .HasQueryFilter(e => IsSuperAdmin || e.UserId == CurrentUserId);

        modelBuilder.Entity<UserConfig>()
            .HasQueryFilter(e => IsSuperAdmin || e.UserId == CurrentUserId);

        modelBuilder.Entity<UserPreference>()
            .HasQueryFilter(e => IsSuperAdmin || e.UserId == CurrentUserId);

        modelBuilder.Entity<ChannelSettings>()
            .HasQueryFilter(e => IsSuperAdmin || e.UserId == CurrentUserId);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Convert all DateTime to UTC for PostgreSQL compatibility
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }
}

internal class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

internal class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
