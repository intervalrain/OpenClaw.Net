---
title: 基礎設施模式指南
description: Unit of Work、Distributed Cache、Object Store、SAGA、Observability 的實作指南
keywords: [Unit of Work, Distributed Cache, Object Store, SAGA, Observability, NATS, OpenTelemetry]
sidebar_position: 13
---

# 基礎設施模式指南

> 學習 Weda.Core 提供的基礎設施模式

## 概觀

Weda.Core 提供以下基礎設施模式：

| 模式 | 用途 | 技術 |
|------|------|------|
| Unit of Work | 交易邊界管理 | EF Core + Pipeline Behavior |
| Distributed Cache | 分散式快取 | NATS KV Store |
| Object Store | Blob 儲存 | NATS Object Store |
| SAGA Pattern | 分散式交易 | Orchestration + Compensation |
| Observability | 追蹤與監控 | OpenTelemetry |

```
src/Weda.Core/
├── Application/
│   ├── Behaviors/
│   │   └── UnitOfWorkBehavior.cs     # 自動 SaveChanges
│   ├── Interfaces/
│   │   ├── IUnitOfWork.cs            # 交易邊界介面
│   │   ├── ISagaStateStore.cs        # SAGA 狀態儲存
│   │   └── Storage/
│   │       └── IBlobStorage.cs       # Blob 儲存介面
│   └── Sagas/
│       ├── ISaga.cs                  # SAGA 定義
│       ├── ISagaStep.cs              # SAGA 步驟
│       ├── SagaState.cs              # SAGA 狀態
│       └── SagaStatus.cs             # 狀態枚舉
├── Infrastructure/
│   ├── Messaging/Nats/
│   │   ├── Caching/                  # 分散式快取
│   │   ├── ObjectStore/              # Object Store
│   │   └── Configuration/
│   ├── Sagas/
│   │   ├── SagaOrchestrator.cs       # SAGA 執行引擎
│   │   └── CacheSagaStateStore.cs    # 狀態持久化
│   └── Observability/
│       ├── ObservabilityOptions.cs   # 設定選項
│       └── ObservabilityExtensions.cs # DI 擴展
```

---

## 1. Unit of Work Pattern

### 1.1 概念

Unit of Work 確保一個業務操作中的所有變更在同一個交易中完成：

```
Command Handler
    ↓
Repository.AddAsync()     ← 只標記變更
Repository.UpdateAsync()  ← 只標記變更
    ↓
UnitOfWorkBehavior
    ↓
SaveChangesAsync()        ← 一次性提交所有變更
```

### 1.2 IUnitOfWork 介面

```csharp
namespace Weda.Core.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

`WedaDbContext` 實作此介面：

```csharp
public abstract class WedaDbContext : DbContext, IUnitOfWork
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 收集 Domain Events
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
           .SelectMany(entry => entry.Entity.PopDomainEvents())
           .ToList();

        // 儲存變更
        var result = await base.SaveChangesAsync(cancellationToken);

        // 發布 Domain Events
        await PublishDomainEvents(domainEvents);

        return result;
    }
}
```

### 1.3 UnitOfWorkBehavior

Pipeline Behavior 在 Command 完成後自動呼叫 `SaveChangesAsync()`：

```csharp
public class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);

        // 只對 Command 執行 SaveChanges
        if (IsCommand())
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return response;
    }

    private static bool IsCommand()
    {
        return typeof(TRequest).GetInterfaces()
            .Any(i => i.IsGenericType &&
                      i.GetGenericTypeDefinition() == typeof(ICommand<>));
    }
}
```

### 1.4 Pipeline 執行順序

```
Request
    ↓
AuthorizationBehavior  ← 檢查權限
    ↓
ValidationBehavior     ← 驗證輸入
    ↓
Handler                ← 業務邏輯
    ↓
UnitOfWorkBehavior     ← SaveChanges (僅 Command)
    ↓
Response
```

### 1.5 Repository 實作

Repository 只標記變更，不直接儲存：

```csharp
public class GenericRepository<T, TId, TDbContext> : IRepository<T, TId>
{
    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        // 不呼叫 SaveChangesAsync()
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        return Task.CompletedTask;
        // 不呼叫 SaveChangesAsync()
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
        // 不呼叫 SaveChangesAsync()
    }
}
```

---

## 2. Distributed Cache (NATS KV)

### 2.1 概念

使用 NATS KV Store 實作 `IDistributedCache`，提供分散式快取能力：

```
Application
    ↓
IDistributedCache (標準 .NET 介面)
    ↓
NatsKvDistributedCache (實作)
    ↓
NATS KV Store
```

### 2.2 啟用快取

在 `Program.cs` 中設定：

```csharp
services.AddNats(builder =>
{
    builder.BindConfiguration(natsOptions);
    builder.AddKeyValueCache(opts =>
    {
        opts.BucketName = "app-cache";      // KV bucket 名稱
        opts.DefaultTtl = TimeSpan.FromMinutes(30);  // 預設 TTL
        opts.ConnectionName = null;          // 使用 default connection
    });
});
```

### 2.3 使用快取

注入 `IDistributedCache` 使用：

```csharp
public class GetEmployeeQueryHandler(
    IEmployeeRepository repository,
    IDistributedCache cache)
{
    public async ValueTask<ErrorOr<EmployeeDto>> Handle(
        GetEmployeeQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"employee:{request.Id}";

        // 嘗試從快取取得
        var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<EmployeeDto>(cached)!;
        }

        // 從資料庫取得
        var employee = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound(request.Id);
        }

        var dto = EmployeeMapper.ToDto(employee);

        // 寫入快取
        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(dto),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            },
            cancellationToken);

        return dto;
    }
}
```

### 2.4 NatsKvCacheOptions

| 屬性 | 預設值 | 說明 |
|------|--------|------|
| `BucketName` | `"cache"` | NATS KV bucket 名稱 |
| `ConnectionName` | `null` | NATS connection 名稱（null = default） |
| `DefaultTtl` | `1 hour` | 預設過期時間 |

---

## 3. Outbox Pattern

### 3.1 概念

Outbox Pattern 確保訊息可靠發布到 NATS JetStream：

```
Transaction
    ↓
1. 儲存業務資料
2. 寫入 OutboxMessage 表
    ↓
Commit
    ↓
OutboxProcessor (背景服務)
    ↓
發布到 NATS JetStream
    ↓
標記為已處理
```

### 3.2 啟用 Outbox

在 `Program.cs` 中設定：

```csharp
services.AddNats(builder =>
{
    builder.BindConfiguration(natsOptions);
    builder.AddOutbox<AppDbContext>(opts =>
    {
        opts.MaxRetries = 5;
        opts.BatchSize = 100;
        opts.ProcessingInterval = TimeSpan.FromSeconds(5);
        opts.RetentionPeriod = TimeSpan.FromDays(7);
    });
});
```

### 3.3 OutboxMessage Entity

```csharp
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }         // Subject/Topic
    public string Payload { get; private set; }      // JSON 序列化內容
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public OutboxMessageStatus Status { get; private set; }

    public static OutboxMessage Create(string type, string payload);
    public void MarkAsProcessed();
    public void MarkAsFailed(string error, int maxRetries);
}

public enum OutboxMessageStatus
{
    Pending = 0,
    Processed = 1,
    DeadLettered = 2
}
```

### 3.4 寫入 Outbox

在 Domain Event Handler 中寫入 Outbox：

```csharp
public class EmployeeCreatedEventHandler(AppDbContext dbContext)
    : INotificationHandler<EmployeeCreatedEvent>
{
    public async ValueTask Handle(
        EmployeeCreatedEvent notification,
        CancellationToken cancellationToken)
    {
        var integrationEvent = new EmployeeCreatedIntegrationEvent
        {
            EmployeeId = notification.Employee.Id,
            Name = notification.Employee.Name,
            Email = notification.Employee.Email.Value
        };

        var outboxMessage = OutboxMessage.Create(
            type: "employee.created",
            payload: JsonSerializer.Serialize(integrationEvent));

        dbContext.Set<OutboxMessage>().Add(outboxMessage);
        // SaveChanges 由 UnitOfWork 處理
    }
}
```

### 3.5 Exponential Backoff

失敗重試採用指數退避策略：

| 重試次數 | 延遲時間 |
|----------|----------|
| 1 | 2 秒 |
| 2 | 4 秒 |
| 3 | 8 秒 |
| 4 | 16 秒 |
| 5 | Dead Letter |

```csharp
public void MarkAsFailed(string error, int maxRetries)
{
    Error = error;
    RetryCount++;

    if (RetryCount >= maxRetries)
    {
        Status = OutboxMessageStatus.DeadLettered;
        NextRetryAt = null;
    }
    else
    {
        // Exponential backoff: 2^retryCount seconds
        var delay = TimeSpan.FromSeconds(Math.Pow(2, RetryCount));
        NextRetryAt = DateTime.UtcNow.Add(delay);
    }
}
```

### 3.6 Circuit Breaker

使用 `Microsoft.Extensions.Resilience` 實作 Circuit Breaker：

```csharp
private static ResiliencePipeline CreateResiliencePipeline(OutboxOptions options)
{
    return new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = options.FailureRatio,      // 50%
            SamplingDuration = options.SamplingDuration, // 30s
            BreakDuration = options.BreakDuration        // 30s
        })
        .Build();
}
```

Circuit Breaker 狀態：

```
Closed → (失敗率超過 50%) → Open → (30秒後) → Half-Open → (成功) → Closed
                                                    ↓
                                               (失敗) → Open
```

### 3.7 OutboxOptions

| 屬性 | 預設值 | 說明 |
|------|--------|------|
| `MaxRetries` | `5` | 最大重試次數 |
| `BatchSize` | `100` | 每次處理的訊息數量 |
| `ProcessingInterval` | `5s` | 處理間隔 |
| `RetentionPeriod` | `7 days` | 已處理訊息保留時間 |
| `DeleteProcessedMessages` | `true` | 是否自動刪除已處理訊息 |
| `FailureRatio` | `0.5` | Circuit Breaker 失敗率閾值 |
| `SamplingDuration` | `30s` | Circuit Breaker 取樣時間 |
| `BreakDuration` | `30s` | Circuit Breaker 斷開時間 |

### 3.8 DbContext 設定

在 `AppDbContext` 的 `OnModelCreating` 中設定 `OutboxMessage`：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.ConfigureOutboxMessage();
}
```

或使用 extension method：

```csharp
public static class OutboxMessageModelBuilderConfiguration
{
    public static ModelBuilder ConfigureOutboxMessage(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).HasMaxLength(500).IsRequired();
            builder.Property(x => x.Payload).IsRequired();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.HasIndex(x => new { x.Status, x.NextRetryAt });
        });

        return modelBuilder;
    }
}
```

---

## 4. 完整設定範例

### 4.1 Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

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
            options.OpenApiInfo = new OpenApiInfo
            {
                Title = "Weda API",
                Version = "v1",
            };
        },
        nats =>
        {
            // 分散式快取
            nats.AddKeyValueCache(opts =>
            {
                opts.BucketName = "app-cache";
                opts.DefaultTtl = TimeSpan.FromMinutes(30);
            });

            // Outbox Pattern
            nats.AddOutbox<AppDbContext>(opts =>
            {
                opts.MaxRetries = 5;
                opts.RetentionPeriod = TimeSpan.FromDays(7);
            });
        }
    );
```

### 4.2 appsettings.json

```json
{
  "Nats": {
    "DefaultConnection": "default",
    "Connections": {
      "default": {
        "Url": "nats://localhost:4222"
      }
    }
  },
  "Outbox": {
    "MaxRetries": 5,
    "BatchSize": 100,
    "ProcessingInterval": "00:00:05",
    "RetentionPeriod": "7.00:00:00"
  }
}
```

---

## 快速參考

### Pattern 選擇

| 需求 | Pattern |
|------|---------|
| 確保一個操作中的所有變更一起提交 | Unit of Work |
| 減少資料庫查詢，提升效能 | Distributed Cache |
| 確保訊息可靠發布到外部系統 | Outbox Pattern |

### 錯誤處理流程

```
Outbox Message
    ↓
發布失敗 → Exponential Backoff (2s, 4s, 8s, 16s, 32s)
    ↓
超過 MaxRetries → Dead Letter Queue
    ↓
Circuit Breaker Open → 暫停處理 30s
```

---

## 4. Object Store (IBlobStorage)

### 4.1 概念

使用 NATS Object Store 儲存大型 Blob 資料：

```
Application
    ↓
IBlobStorage (介面)
    ↓
NatsObjectStorage (實作)
    ↓
NATS Object Store
```

### 4.2 啟用 Object Store

在 `Program.cs` 中設定：

```csharp
services.AddWedaCore<...>(
    builder.Configuration,
    // ...
    nats =>
    {
        nats.AddObjectStore(opts =>
        {
            opts.BucketName = "blobs";
        });
    }
);
```

### 4.3 IBlobStorage 介面

```csharp
public interface IBlobStorage
{
    Task<ErrorOr<BlobInfo>> PutAsync<T>(string key, T value, CancellationToken ct = default);
    Task<ErrorOr<T>> GetAsync<T>(string key, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public record BlobInfo(string Key, ulong Size, DateTime ModifiedAt);
```

### 4.4 使用範例

```csharp
public class DocumentService(IBlobStorage storage)
{
    public async Task<ErrorOr<BlobInfo>> UploadAsync(string key, Document doc)
    {
        return await storage.PutAsync(key, doc);
    }

    public async Task<ErrorOr<Document>> DownloadAsync(string key)
    {
        return await storage.GetAsync<Document>(key);
    }
}
```

### 4.5 序列化

`NatsObjectStorage` 根據型別自動選擇序列化方式：

| 型別 | 序列化方式 |
|------|-----------|
| `byte[]` | 直接儲存 |
| `Stream` | 複製到 byte[] |
| 其他物件 | JSON 序列化 |

---

## 5. SAGA Pattern

### 5.1 概念

SAGA Pattern 用於處理跨服務的分散式交易，透過 Compensation 機制確保一致性：

```
Step1 ──✓──> Step2 ──✓──> Step3 ──✗──> Compensate
  │           │           │              ↓
  └───────────┴───────────┴──────── Rollback All
```

### 5.2 啟用 SAGA

```csharp
services.AddSagas();
```

### 5.3 核心介面

```csharp
// 單一步驟
public interface ISagaStep<TData> where TData : class
{
    string Name { get; }
    Task<ErrorOr<TData>> ExecuteAsync(TData data, CancellationToken ct = default);
    Task<ErrorOr<TData>> CompensateAsync(TData data, CancellationToken ct = default);
}

// SAGA 定義
public interface ISaga<TData> where TData : class
{
    string SagaType { get; }
    IReadOnlyList<ISagaStep<TData>> Steps { get; }
}
```

### 5.4 實作範例

**1. 定義 Data Model：**

```csharp
public class OrderSagaData
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? PaymentId { get; set; }
}
```

**2. 實作 Steps：**

```csharp
public class CreateOrderStep : ISagaStep<OrderSagaData>
{
    public string Name => "CreateOrder";

    public async Task<ErrorOr<OrderSagaData>> ExecuteAsync(OrderSagaData data, CancellationToken ct)
    {
        data.OrderId = Guid.NewGuid().ToString();
        return data;
    }

    public async Task<ErrorOr<OrderSagaData>> CompensateAsync(OrderSagaData data, CancellationToken ct)
    {
        // 取消訂單邏輯
        return data;
    }
}

public class ProcessPaymentStep(IPaymentService payment) : ISagaStep<OrderSagaData>
{
    public string Name => "ProcessPayment";

    public async Task<ErrorOr<OrderSagaData>> ExecuteAsync(OrderSagaData data, CancellationToken ct)
    {
        var result = await payment.ChargeAsync(data.Amount, ct);
        if (result.IsError) return result.FirstError;

        data.PaymentId = result.Value;
        return data;
    }

    public async Task<ErrorOr<OrderSagaData>> CompensateAsync(OrderSagaData data, CancellationToken ct)
    {
        if (data.PaymentId is not null)
            await payment.RefundAsync(data.PaymentId, ct);
        return data;
    }
}
```

**3. 定義 SAGA：**

```csharp
public class OrderSaga(
    CreateOrderStep createOrder,
    ProcessPaymentStep processPayment) : ISaga<OrderSagaData>
{
    public string SagaType => "OrderSaga";
    public IReadOnlyList<ISagaStep<OrderSagaData>> Steps => [createOrder, processPayment];
}
```

**4. 執行 SAGA：**

```csharp
public class CreateOrderHandler(SagaOrchestrator orchestrator, OrderSaga saga)
{
    public async Task<ErrorOr<string>> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var data = new OrderSagaData { Amount = cmd.Amount };
        var result = await orchestrator.ExecuteAsync(saga, data, ct: ct);

        return result.Match(
            success => success.OrderId,
            errors => errors
        );
    }
}
```

### 5.5 狀態追蹤

SAGA 狀態透過 `IDistributedCache` (NATS KV) 持久化：

| 狀態 | 說明 |
|------|------|
| `Pending` | 尚未開始 |
| `Running` | 執行中 |
| `Completed` | 成功完成 |
| `Failed` | 執行失敗 |
| `Compensating` | 補償中 |
| `Compensated` | 補償完成 |

---

## 6. Observability (OpenTelemetry)

### 6.1 概念

使用 OpenTelemetry 實現分散式追蹤和監控：

```
Application
    ↓
OpenTelemetry SDK
    ↓
┌───────────┬───────────┐
│  Console  │   OTLP    │
│ (開發用)   │ (生產用)   │
└───────────┴───────────┘
```

### 6.2 啟用 Observability

在 `Program.cs` 中設定：

```csharp
services.AddWedaCore<...>(
    builder.Configuration,
    // ...
    options =>
    {
        options.Observability.ServiceName = "MyService";
        options.Observability.ServiceVersion = "1.0.0";

        // 開發環境：Console Exporter
        options.Observability.Tracing.UseConsoleExporter = true;

        // 生產環境：OTLP Exporter
        // options.Observability.Tracing.OtlpEndpoint = "http://otel-collector:4317";
    }
);
```

### 6.3 ObservabilityOptions

```csharp
public class ObservabilityOptions
{
    public string ServiceName { get; set; } = "WedaService";
    public string? ServiceVersion { get; set; }
    public TracingOptions Tracing { get; set; } = new();
    public MetricsOptions Metrics { get; set; } = new();
}

public class TracingOptions
{
    public bool Enabled { get; set; } = true;
    public bool UseConsoleExporter { get; set; } = false;
    public string? OtlpEndpoint { get; set; }
}

public class MetricsOptions
{
    public bool Enabled { get; set; } = true;
    public bool UseConsoleExporter { get; set; } = false;
    public string? OtlpEndpoint { get; set; }
}
```

### 6.4 自動 Instrumentation

已自動追蹤：
- ✅ **ASP.NET Core** - 所有 HTTP requests
- ✅ **HttpClient** - 所有 outgoing HTTP calls

### 6.5 手動建立 Span

```csharp
using System.Diagnostics;

public class MyService
{
    private static readonly ActivitySource Source = new("MyService");

    public async Task DoWorkAsync()
    {
        using var activity = Source.StartActivity("DoWork");
        activity?.SetTag("user.id", "123");

        // your logic here

        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
```

需要在 `ObservabilityExtensions` 註冊 source：

```csharp
tracing.AddSource("MyService");
```

---

## 快速參考

### Pattern 選擇

| 需求 | Pattern |
|------|---------|
| 確保一個操作中的所有變更一起提交 | Unit of Work |
| 減少資料庫查詢，提升效能 | Distributed Cache |
| 確保訊息可靠發布到外部系統 | Outbox Pattern |
| 儲存大型檔案或 Blob | Object Store |
| 跨服務分散式交易 | SAGA Pattern |
| 追蹤與監控 | Observability |

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [09-error-handling-guide.md](09-error-handling-guide.md) - 錯誤處理（ErrorOr Pattern）
- [11-domain-events-guide.md](11-domain-events-guide.md) - Domain Events
- [Microsoft.Extensions.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/)
- [NATS KV Store](https://docs.nats.io/nats-concepts/jetstream/key-value-store)
- [NATS Object Store](https://docs.nats.io/nats-concepts/jetstream/obj_store)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)