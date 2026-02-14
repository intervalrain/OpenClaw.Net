---
title: Infrastructure Layer 實作指南
description: Repository、Database Context 與外部服務整合的逐步實作指南
keywords: [Infrastructure Layer, Repository, Entity Framework, DbContext, Dependency Injection]
sidebar_position: 4
---

# Infrastructure Layer 實作指南

> 學習如何實作 Repository、資料庫持久化與外部服務整合

## 概觀

Infrastructure Layer 實作 Application 與 Domain Layer 中定義的技術細節，處理資料庫存取、外部服務與 Cross-Cutting Concern。

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
        // 從此 Assembly 套用所有 Configuration
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
```

### 1.2 WedaDbContext Base Class（來自 Weda.Core）

Base Class 提供：
- 自動提取與發布 Domain Event
- 所有 DateTime 屬性的 UTC 轉換
- Domain Event 離線佇列機制

```csharp
public abstract class WedaDbContext : DbContext
{
    private readonly IMediator _mediator;

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // 儲存前提取 Domain Event
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // 成功儲存後發布 Domain Event
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        // 將所有 DateTime 轉換為 UTC
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
        // 資料表名稱
        builder.ToTable("Employees");

        // 主鍵
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

        // Value Object: Email 含唯一索引
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

        // 簡單屬性
        builder.Property(e => e.Position)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.HireDate)
            .IsRequired();

        // Enum 轉為字串
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // 自我參照關聯
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.SupervisorId)
            .OnDelete(DeleteBehavior.SetNull);

        // 時間戳記
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt);
    }
}
```

### 2.2 Value Converter Pattern

```csharp
// 為複雜的 Value Object 建立可重複使用的 Converter
public class EmailConverter : ValueConverter<Email, string>
{
    public EmailConverter()
        : base(
            v => v.Value,
            v => Email.Create(v).Value)
    {
    }
}

// 在 Configuration 中使用
builder.Property(e => e.Email)
    .HasConversion(new EmailConverter())
    .HasMaxLength(Email.MaxLength);
```

### Configuration 檢查清單

- [ ] 指定資料表名稱
- [ ] 設定主鍵與值產生方式
- [ ] Value Object 有轉換設定
- [ ] 標記必填屬性
- [ ] 指定最大長度
- [ ] 為唯一/可搜尋欄位建立索引
- [ ] 設定關聯與刪除行為

---

## 3. Repository 實作

### 3.1 GenericRepository Base Class

```csharp
// 來自 Weda.Core
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

### 3.2 特化的 Repository

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

## 4. 資料庫設定

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

### 4.3 多資料庫支援

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

## 5. Security Service

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

        // 檢查 Role
        if (requiredRoles.Count > 0 &&
            !requiredRoles.Any(r => currentUser.Roles.Contains(r)))
        {
            return Error.Forbidden(description: "Insufficient role");
        }

        // 檢查 Permission
        if (requiredPermissions.Count > 0 &&
            !requiredPermissions.Any(p => currentUser.Permissions.Contains(p)))
        {
            return Error.Forbidden(description: "Insufficient permissions");
        }

        // 檢查 Policy
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

### 6.1 Module 註冊

```csharp
public static class WedaTemplateInfrastructureModule
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDatabase(configuration);

        // Repository
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();

        // Domain Service
        services.AddScoped<EmployeeHierarchyManager>();

        // Security
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IPolicyEnforcer, PolicyEnforcer>();

        // Service
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

### Service Lifetime 參考

| Lifetime | 使用情境 | 範例 |
|----------|----------|------|
| Singleton | Stateless、Thread-Safe 的 Service | `IDateTimeProvider` |
| Scoped | 每次 Request 的 Service | `IRepository`、`ICurrentUserProvider` |
| Transient | 輕量、Stateless | 很少使用 |

---

## 7. 外部服務

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

// 註冊
services.Configure<JwtSettings>(
    configuration.GetSection(JwtSettings.SectionName));

// 透過 Constructor Injection 使用
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

## 快速參考

### 檔案命名慣例

| 元件 | 模式 | 範例 |
|------|------|------|
| DbContext | `AppDbContext.cs` | `AppDbContext.cs` |
| Configuration | `{Entity}Configuration.cs` | `EmployeeConfiguration.cs` |
| Repository | `{Entity}Repository.cs` | `EmployeeRepository.cs` |
| Options | `{Feature}Options.cs` | `DatabaseOptions.cs` |
| Service | `{Name}Service.cs` 或 `{Name}Provider.cs` | `AuthorizationService.cs` |
| Module | `{Project}InfrastructureModule.cs` | `WedaTemplateInfrastructureModule.cs` |

### 資料夾結構

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

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [02-application-layer.md](02-application-layer.md) - Application Layer 指南
- [04-api-layer.md](04-api-layer.md) - API Layer 指南
- [Entity Framework Core Documentation](https://learn.microsoft.com/ef/core/)
