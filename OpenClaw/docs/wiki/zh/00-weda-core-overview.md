---
title: Weda.Core 概觀
description: 提供 DDD 抽象、CQRS Pipeline、NATS 訊息傳遞與 API 基礎設施的核心函式庫
keywords: [Weda.Core, DDD, CQRS, NATS, Clean Architecture, Infrastructure]
sidebar_position: 1
---

# Weda.Core 概觀

> 用於建構 Clean Architecture 應用程式的共用基礎設施函式庫，支援 DDD、CQRS 與 Event-Driven Messaging

## 什麼是 Weda.Core？

Weda.Core 是一個基礎函式庫，提供建構遵循 Clean Architecture 與 Domain-Driven Design 原則的應用程式所需的所有 Base Class、抽象與基礎設施。它消除了重複的樣板程式碼，並確保專案間的一致性。

```
Weda.Core/
├── Domain/              # DDD Base Class (Entity, AggregateRoot 等)
├── Application/         # CQRS Behavior、Security、Interface
├── Infrastructure/      # Persistence、NATS Messaging、Middleware
├── Api/                 # REST Controller Base、Swagger 設定
└── WedaCoreModule.cs    # Service 註冊與 Middleware 設定
```

---

## 架構概觀

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Your Application                             │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────┐  ┌─────────────┐  ┌───────────────┐  ┌───────────┐  │
│  │   Domain    │  │ Application │  │ Infrastructure│  │    Api    │  │
│  │   Layer     │  │   Layer     │  │     Layer     │  │   Layer   │  │
│  └──────┬──────┘  └──────┬──────┘  └───────┬───────┘  └─────┬─────┘  │
│         │                │                 │                │        │
├─────────┴────────────────┴─────────────────┴────────────────┴────────┤
│                           Weda.Core                                  │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐  ┌───────────┐  │
│  │   Entity     │  │  Behaviors   │  │  DbContext  │  │    Api    │  │
│  │ AggregateRoot│  │ Validation   │  │  Repository │  │ Controller│  │
│  │ IDomainEvent │  │ Authorization│  │    NATS     │  │  Swagger  │  │
│  │ IRepository  │  │  Security    │  │ Middleware  │  │  Filters  │  │
│  └──────────────┘  └──────────────┘  └─────────────┘  └───────────┘  │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 1. Domain 抽象

Weda.Core 提供 Domain-Driven Design 的基礎：

### Entity<TId>

所有 Domain Entity 的 Base Class，具備基於 Identity 的相等性比較。

```csharp
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected init; }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> entity && Id.Equals(entity.Id);

    public override int GetHashCode() => Id.GetHashCode();
}
```

### AggregateRoot<TId>

作為 Aggregate 邊界的 Entity，支援 Domain Event。

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public List<IDomainEvent> PopDomainEvents()
    {
        var copy = _domainEvents.ToList();
        _domainEvents.Clear();
        return copy;
    }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
```

### IRepository<T, TId>

資料存取抽象的 Generic Repository Interface。

```csharp
public interface IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
{
    Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}
```

### IDomainEvent

Domain Event 的 Marker Interface，繼承 Mediator 的 `INotification`。

```csharp
public interface IDomainEvent : INotification { }
```

---

## 2. Application Layer 元件

### Pipeline Behavior

Weda.Core 提供 Mediator Pipeline Behavior 處理 Cross-Cutting Concern：

**ValidationBehavior**
- 在 Handler 執行前攔截所有 Request
- 使用 FluentValidation 驗證 Request 物件
- 驗證失敗時回傳包含驗證錯誤的 `ErrorOr`

**AuthorizationBehavior**
- 攔截 `IAuthorizeableRequest<T>` 實作
- 讀取 `[Authorize]` Attribute 取得所需的 Role、Permission、Policy
- 委派給 `IAuthorizationService` 進行授權檢查

### Security 基礎設施

```csharp
// 宣告式授權的 Attribute
[Authorize(Roles = "Admin", Permissions = "employees:write")]
public record CreateEmployeeCommand(...) : IAuthorizeableRequest<ErrorOr<EmployeeDto>>;

// Authorization Service 的 Interface
public interface IAuthorizationService
{
    ErrorOr<Success> AuthorizeCurrentUser<T>(
        IAuthorizeableRequest<T> request,
        List<string> requiredRoles,
        List<string> requiredPermissions,
        List<string> requiredPolicies);
}

// JWT Token 產生器的 Interface
public interface IJwtTokenGenerator
{
    string GenerateToken(
        int id,
        string name,
        string email,
        List<string> permissions,
        List<string> roles);
}
```

### Application Interface

```csharp
// 可測試時間操作的抽象
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
```

---

## 3. Infrastructure 元件

### WedaDbContext

具備自動 Domain Event 發布與 Eventual Consistency 支援的 Base DbContext。

```csharp
public abstract class WedaDbContext : DbContext
{
    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. 從 Aggregate Root 收集 Domain Event
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        // 2. 儲存變更至資料庫
        var result = await base.SaveChangesAsync(cancellationToken);

        // 3. 佇列或發布 Domain Event
        if (IsUserOnline)
            QueueEventsForEventualConsistency(domainEvents);
        else
            await PublishEventsImmediately(domainEvents);

        return result;
    }
}
```

### GenericRepository<T, TId, TDbContext>

使用 EF Core 的 Base Repository 實作。

```csharp
public class GenericRepository<T, TId, TDbContext> : IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
    where TDbContext : DbContext
{
    protected readonly TDbContext DbContext;
    protected readonly DbSet<T> DbSet;

    // 具備自動 SaveChanges 的標準 CRUD 操作
}
```

### Eventual Consistency Middleware

確保 Domain Event 在同一個 Transaction 內發布。

```
HTTP Request
     ↓
Begin Transaction
     ↓
Controller → Handler → Repository.SaveChanges()
     ↓
Domain Event 佇列於 HttpContext
     ↓
Response 送回 Client
     ↓
發布佇列中的 Domain Event
     ↓
Commit Transaction
```

### NATS Messaging

完整的 Event-Driven Messaging 基礎設施，支援多種模式：

| Pattern | 說明 |
|---------|------|
| Request-Reply | 同步 RPC 風格的通訊 |
| Core Pub-Sub | Fire-and-Forget 訊息傳遞 |
| JetStream Consume | 持久化、保證遞送的持續消費 |
| JetStream Fetch | 批次訊息處理 |

---

## 4. API 元件

### ApiController

具備自動錯誤對應至 ProblemDetails 的 Base Controller。

```csharp
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public class ApiController : ControllerBase
{
    protected ActionResult Problem(List<Error> errors)
    {
        // 將 ErrorOr Error 對應至適當的 HTTP Status Code
        // - Validation → 400 Bad Request
        // - NotFound → 404 Not Found
        // - Conflict → 409 Conflict
        // - Unauthorized → 401 Unauthorized
        // - Forbidden → 403 Forbidden
    }
}
```

### EventController

NATS Event-Driven Endpoint 的 Base Class。

```csharp
[Stream("employees_v1_stream")]
[Consumer("employees_v1_consumer")]
[Connection("bus")]
public abstract class EventController
{
    public IMediator Mediator { get; }
    public INatsConnectionProvider NatsProvider { get; }
    public ILogger Logger { get; }
    public string Subject { get; }
    public IReadOnlyDictionary<string, string> SubjectValues { get; }
}
```

### Swagger 整合

- 自動產生 OpenAPI 文件
- 透過 `IExamplesProvider<T>` 注入 Request/Response 範例
- 為授權 Endpoint 加入 Bearer Token Security

---

## 5. Module 註冊

### 將 Weda.Core 加入您的應用程式

```csharp
// Program.cs
using System.Reflection;
using Microsoft.OpenApi;
using Weda.Core;
using OpenClaw.Api;
using OpenClaw.Application;
using OpenClaw.Contracts;
using OpenClaw.Infrastructure;
using OpenClaw.Infrastructure.Common.Persistence;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddWedaCore<IAssemblyMarker, IContractsMarker, IApplicationMarker>(
            builder.Configuration,
            services => services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.Assemblies = [typeof(IApplicationMarker).Assembly];
            }),
            options =>
            {
                options.XmlCommentAssemblies = [Assembly.GetExecutingAssembly()];
                options.OpenApiInfo = new OpenApiInfo
                {
                    Title = "Weda API",
                    Version = "v1",
                };
            });
}

var app = builder.Build();
{
    app.UseWedaCore<AppDbContext>(options =>
    {
        options.EnsureDatabaseCreated = false;
        options.SwaggerEndpointUrl = "/swagger/v1/swagger.json";
        options.SwaggerEndpointName = "Weda API V1";
        options.RoutePrefix = "swagger";
    });

    app.Run();
}
```

### AddWedaCore 參數

| 參數 | 說明 |
|------|------|
| `TApiMarker` | API Assembly Marker，用於掃描 EventController |
| `TContractsMarker` | Contracts Assembly Marker，用於 Swagger 範例 |
| `TApplicationMarker` | Application Assembly Marker，用於 Validator |
| `configuration` | IConfiguration，用於讀取設定 |
| `mediatorAction` | 設定 Mediator 選項的 Callback |
| `optionsAction` | 設定 WedaCoreOptions 的 Callback |

### WedaCoreOptions (AddWedaCore)

| 選項 | 說明 |
|------|------|
| `XmlCommentAssemblies` | 包含 XML 註解的 Assembly，供 Swagger 使用 |
| `OpenApiInfo` | OpenAPI 文件資訊（標題、版本） |

### WedaCoreMiddlewareOptions (UseWedaCore)

| 選項 | 說明 |
|------|------|
| `EnsureDatabaseCreated` | 啟動時自動建立資料庫 |
| `SwaggerEndpointUrl` | Swagger JSON Endpoint URL |
| `SwaggerEndpointName` | Swagger Endpoint 顯示名稱 |
| `RoutePrefix` | Swagger UI Route Prefix |

---

## 6. 關鍵相依套件

Weda.Core 整合以下函式庫（皆為 MIT License）：

| 函式庫 | 用途 |
|--------|------|
| **ErrorOr** | Functional Error Handling |
| **Mediator** | CQRS 與 Pipeline Behavior（基於 Source Generator，高效能） |
| **Mapperly** | 物件對應，透過 Source Generator（零 Reflection） |
| **FluentValidation** | Request 驗證 |
| **NATS.Net** | Messaging 與 Event Streaming |
| **Entity Framework Core** | 資料庫持久化 |
| **Asp.Versioning** | API Versioning |
| **Swashbuckle** | OpenAPI/Swagger 文件 |

---

## 7. 實作的設計模式

| Pattern | 實作 |
|---------|------|
| Domain-Driven Design | Entity、AggregateRoot、Domain Event、Repository |
| CQRS | 透過 Mediator 分離 Command/Query |
| Repository | Generic 與特化的 Repository 抽象 |
| Eventual Consistency | 基於 Middleware 的 Domain Event 發布 |
| Pipeline | Validation 與 Authorization Behavior |
| Factory Method | 使用 ErrorOr 的 Entity 建立 |
| Event-Driven | Domain Event 與 NATS Messaging |

---

## 相關資源

- [01-domain-layer.md](01-domain-layer.md) - Domain Layer 實作指南
- [02-application-layer.md](02-application-layer.md) - Application Layer 指南
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer 指南
- [04-api-layer.md](04-api-layer.md) - API Layer 指南
- [GUIDE.md](GUIDE.md) - 學習指南總覽
