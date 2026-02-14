---
title: Infrastructure Layer Implementation Guide
description: Step-by-step guide to implementing repositories, database context, and external services
keywords: [Infrastructure Layer, Repository, Entity Framework, DbContext, Dependency Injection]
sidebar_position: 4
---

# Infrastructure Layer Implementation Guide

> Learn how to implement repositories, database persistence, and external service integrations

## Overview

The Infrastructure Layer implements technical concerns defined in Application and Domain layers. It handles database access, external services, and cross-cutting concerns.

```
src/OpenClaw.Infrastructure/
├── Common/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
│       └── EventualConsistencyMiddleware.cs
├── Employees/
│   └── Persistence/
│       ├── EmployeeRepository.cs
│       └── EmployeeConfiguration.cs
├── Persistence/
│   ├── DatabaseOptions.cs
│   └── DatabaseProvider.cs
├── Security/
│   ├── JwtTokenGenerator.cs
│   ├── JwtSettings.cs
│   ├── AuthorizationService.cs
│   ├── CurrentUserProvider/
│   └── PolicyEnforcer/
├── Services/
│   └── SystemDateTimeProvider.cs
└── WedaTemplateInfrastructureModule.cs
```

---

## 1. Database Context

### 1.1 AppDbContext

```csharp
public class AppDbContext : WedaDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
```

### 1.2 WedaDbContext Base Class (from Weda.Core)

The base class provides:
- Automatic domain event extraction and publishing
- UTC DateTime conversion for all DateTime properties
- Domain event queuing for offline scenarios

```csharp
public abstract class WedaDbContext : DbContext
{
    private readonly IMediator _mediator;

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // Extract domain events before saving
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Publish domain events after successful save
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        // Convert all DateTime to UTC
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
    }
}
```

---

## 2. Entity Configuration

### 2.1 IEntityTypeConfiguration

```csharp
public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        // Table name
        builder.ToTable("Employees");

        // Primary key
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // Value Object: EmployeeName
        builder.Property(e => e.Name)
            .HasConversion(
                v => v.Value,
                v => EmployeeName.Create(v).Value)
            .HasMaxLength(EmployeeName.MaxLength)
            .IsRequired();

        // Value Object: Email with unique index
        builder.Property(e => e.Email)
            .HasConversion(
                v => v.Value,
                v => Email.Create(v).Value)
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.HasIndex(e => e.Email)
            .IsUnique();

        // Value Object: Department
        builder.Property(e => e.Department)
            .HasConversion(
                v => v.Value,
                v => Department.Create(v).Value)
            .HasMaxLength(Department.MaxLength)
            .IsRequired();

        // Simple properties
        builder.Property(e => e.Position)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.HireDate)
            .IsRequired();

        // Enum as string
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Self-referencing relationship
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.SupervisorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Timestamps
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt);
    }
}
```

### 2.2 Value Converter Pattern

```csharp
// For complex Value Objects, create reusable converters
public class EmailConverter : ValueConverter<Email, string>
{
    public EmailConverter()
        : base(
            v => v.Value,
            v => Email.Create(v).Value)
    {
    }
}

// Usage in configuration
builder.Property(e => e.Email)
    .HasConversion(new EmailConverter())
    .HasMaxLength(Email.MaxLength);
```

### Configuration Checklist

- [ ] Table name specified
- [ ] Primary key configured with value generation
- [ ] Value Objects have conversion
- [ ] Required properties marked
- [ ] Max lengths specified
- [ ] Indexes created for unique/searchable fields
- [ ] Relationships configured with delete behavior

---

## 3. Repository Implementation

### 3.1 GenericRepository Base Class

```csharp
// From Weda.Core
public class GenericRepository<T, TId, TDbContext> : IRepository<T, TId>
    where T : Entity<TId>
    where TDbContext : DbContext
{
    protected readonly TDbContext DbContext;
    protected readonly DbSet<T> DbSet;

    public GenericRepository(TDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(
        TId id,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<List<T>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task AddAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task UpdateAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
```

### 3.2 Specialized Repository

```csharp
public class EmployeeRepository
    : GenericRepository<Employee, int, AppDbContext>, IEmployeeRepository
{
    public EmployeeRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Employee?> GetByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(
                e => e.Email == email,
                cancellationToken);
    }

    public async Task<List<Employee>> GetBySupervisorIdAsync(
        int supervisorId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(e => e.SupervisorId == supervisorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithEmailAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AnyAsync(
                e => e.Email == email,
                cancellationToken);
    }
}
```

---

## 4. Database Configuration

### 4.1 Database Options

```csharp
public class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
    public string ConnectionString { get; set; } = "Data Source=OpenClaw.sqlite";
    public string DatabaseName { get; set; } = "WedaTemplate";
}

public enum DatabaseProvider
{
    Sqlite,
    PostgreSql,
    MongoDb,
    InMemory
}
```

### 4.2 appsettings.json

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=OpenClaw.sqlite",
    "DatabaseName": "WedaTemplate"
  }
}
```

### 4.3 Multi-Database Support

```csharp
public static IServiceCollection AddDatabase(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var options = configuration
        .GetSection(DatabaseOptions.SectionName)
        .Get<DatabaseOptions>() ?? new DatabaseOptions();

    services.AddDbContext<AppDbContext>(dbOptions =>
    {
        switch (options.Provider)
        {
            case DatabaseProvider.Sqlite:
                dbOptions.UseSqlite(options.ConnectionString);
                break;

            case DatabaseProvider.PostgreSql:
                dbOptions.UseNpgsql(options.ConnectionString);
                break;

            case DatabaseProvider.MongoDb:
                dbOptions.UseMongoDB(
                    options.ConnectionString,
                    options.DatabaseName);
                break;

            case DatabaseProvider.InMemory:
                dbOptions.UseInMemoryDatabase(options.DatabaseName);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported database provider: {options.Provider}");
        }
    });

    return services;
}
```

---

## 5. Security Services

### 5.1 JWT Token Generator

```csharp
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;
    private readonly IDateTimeProvider _dateTimeProvider;

    public JwtTokenGenerator(
        IOptions<JwtSettings> jwtSettings,
        IDateTimeProvider dateTimeProvider)
    {
        _jwtSettings = jwtSettings.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public string GenerateToken(
        int userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim("permissions", p)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Secret));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: _dateTimeProvider.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### 5.2 Current User Provider

```csharp
public class CurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUser? GetCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var userId = int.Parse(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        var email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var permissions = user.FindAll("permissions")
            .Select(c => c.Value)
            .ToList();

        return new CurrentUser(userId, email, roles, permissions);
    }
}
```

### 5.3 Authorization Service

```csharp
public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPolicyEnforcer _policyEnforcer;

    public AuthorizationService(
        ICurrentUserProvider currentUserProvider,
        IPolicyEnforcer policyEnforcer)
    {
        _currentUserProvider = currentUserProvider;
        _policyEnforcer = policyEnforcer;
    }

    public ErrorOr<Success> AuthorizeCurrentUser<T>(
        IAuthorizeableRequest<T> request,
        List<string> requiredRoles,
        List<string> requiredPermissions,
        List<string> requiredPolicies)
    {
        var currentUser = _currentUserProvider.GetCurrentUser();

        if (currentUser is null)
            return Error.Unauthorized(description: "User is not authenticated");

        // Check roles
        if (requiredRoles.Count > 0 &&
            !requiredRoles.Any(r => currentUser.Roles.Contains(r)))
        {
            return Error.Forbidden(description: "Insufficient role");
        }

        // Check permissions
        if (requiredPermissions.Count > 0 &&
            !requiredPermissions.Any(p => currentUser.Permissions.Contains(p)))
        {
            return Error.Forbidden(description: "Insufficient permissions");
        }

        // Check policies
        foreach (var policy in requiredPolicies)
        {
            var result = _policyEnforcer.Enforce(policy, currentUser, request);
            if (result.IsError)
                return result.Errors;
        }

        return Result.Success;
    }
}
```

---

## 6. Infrastructure Module

### 6.1 Module Registration

```csharp
public static class WedaTemplateInfrastructureModule
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDatabase(configuration);

        // Repositories
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();

        // Domain Services
        services.AddScoped<EmployeeHierarchyManager>();

        // Security
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IPolicyEnforcer, PolicyEnforcer>();

        // Services
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<AppDbContext>(dbOptions =>
        {
            _ = options.Provider switch
            {
                DatabaseProvider.Sqlite =>
                    dbOptions.UseSqlite(options.ConnectionString),
                DatabaseProvider.PostgreSql =>
                    dbOptions.UseNpgsql(options.ConnectionString),
                DatabaseProvider.MongoDb =>
                    dbOptions.UseMongoDB(
                        options.ConnectionString,
                        options.DatabaseName),
                DatabaseProvider.InMemory =>
                    dbOptions.UseInMemoryDatabase(options.DatabaseName),
                _ => throw new InvalidOperationException(
                    $"Unsupported provider: {options.Provider}")
            };
        });

        return services;
    }
}
```

### Service Lifetime Reference

| Lifetime | Use Case | Example |
|----------|----------|---------|
| Singleton | Stateless, thread-safe services | `IDateTimeProvider` |
| Scoped | Per-request services | `IRepository`, `ICurrentUserProvider` |
| Transient | Lightweight, stateless | Rarely used |

---

## 7. External Services

### 7.1 DateTime Provider

```csharp
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

### 7.2 Options Pattern

```csharp
public class JwtSettings
{
    public const string SectionName = "Authentication:Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

// Registration
services.Configure<JwtSettings>(
    configuration.GetSection(JwtSettings.SectionName));

// Usage via constructor injection
public class SomeService
{
    private readonly JwtSettings _settings;

    public SomeService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }
}
```

---

## Quick Reference

### File Naming Conventions

| Component | Pattern | Example |
|-----------|---------|---------|
| DbContext | `AppDbContext.cs` | `AppDbContext.cs` |
| Configuration | `{Entity}Configuration.cs` | `EmployeeConfiguration.cs` |
| Repository | `{Entity}Repository.cs` | `EmployeeRepository.cs` |
| Options | `{Feature}Options.cs` | `DatabaseOptions.cs` |
| Service | `{Name}Service.cs` or `{Name}Provider.cs` | `AuthorizationService.cs` |
| Module | `{Project}InfrastructureModule.cs` | `WedaTemplateInfrastructureModule.cs` |

### Folder Structure

```
Infrastructure/
├── Common/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
├── {AggregateName}/
│   └── Persistence/
│       ├── {Entity}Repository.cs
│       └── {Entity}Configuration.cs
├── Persistence/
│   ├── DatabaseOptions.cs
│   └── DatabaseProvider.cs
├── Security/
│   ├── {Service}.cs
│   └── {Feature}/
├── Services/
└── {Project}InfrastructureModule.cs
```

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [02-application-layer.md](02-application-layer.md) - Application Layer Guide
- [04-api-layer.md](04-api-layer.md) - API Layer Guide
- [Entity Framework Core Documentation](https://learn.microsoft.com/ef/core/)
