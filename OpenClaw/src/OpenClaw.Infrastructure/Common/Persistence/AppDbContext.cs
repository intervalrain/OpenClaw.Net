using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.CronJobs.Entities;
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
    /// Current user ID resolved from JWT claims. Used by EF Core global query filters
    /// to automatically scope user-owned entities. Returns Guid.Empty if no user context.
    /// </summary>
    private Guid CurrentUserId =>
        Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirstValue("id"), out var id)
            ? id
            : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        // Global query filters for multi-tenant isolation.
        // These automatically add WHERE UserId = @currentUserId to all queries.
        // Use .IgnoreQueryFilters() for admin/system queries that need to bypass.
        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(e => CurrentUserId == Guid.Empty || e.UserId == CurrentUserId);

        modelBuilder.Entity<CronJob>()
            .HasQueryFilter(e => CurrentUserId == Guid.Empty || e.CreatedByUserId == CurrentUserId);

        modelBuilder.Entity<ToolInstance>()
            .HasQueryFilter(e => CurrentUserId == Guid.Empty || e.CreatedByUserId == CurrentUserId);
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
