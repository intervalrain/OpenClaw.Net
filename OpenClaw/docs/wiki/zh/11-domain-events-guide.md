---
title: Domain Events 指南
description: Domain Event 發布與處理機制的實作指南
keywords: [Domain Events, Event Handlers, Mediator, CQRS]
sidebar_position: 12
---

# Domain Events 指南

> 學習如何使用 Domain Events 實作事件驅動架構

## 概觀

Domain Events 是 DDD 的核心概念之一，用於：
- 解耦 Aggregate 之間的依賴
- 觸發副作用（發送通知、更新快取等）
- 實現最終一致性
- 整合外部系統（NATS、Message Queue）

```
src/
├── Weda.Core/Domain/
│   ├── IDomainEvent.cs             # Domain Event 介面
│   ├── IAggregateRoot.cs           # Aggregate Root 介面
│   └── AggregateRoot.cs            # Aggregate Root 基底類別
├── OpenClaw.Domain/
│   └── {Aggregate}/Events/
│       └── {Event}Event.cs         # Domain Event 定義
└── OpenClaw.Application/
    └── {Feature}/EventHandlers/
        └── {Event}EventHandler.cs  # Event Handler
```

---

## 1. Domain Event 基礎

### 1.1 IDomainEvent 介面

```csharp
using Mediator;

namespace Weda.Core.Domain;

public interface IDomainEvent : INotification
{
}
```

Domain Event 繼承自 Mediator 的 `INotification`，可透過 Mediator 發布。

### 1.2 AggregateRoot 基底類別

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }
    protected AggregateRoot() { }

    // 取出並清空 Domain Events
    public List<IDomainEvent> PopDomainEvents()
    {
        var copy = _domainEvents.ToList();
        _domainEvents.Clear();
        return copy;
    }

    // 在 Aggregate 中觸發事件
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
```

---

## 2. 定義 Domain Event

### 2.1 基本結構

```csharp
namespace OpenClaw.Domain.Employees.Events;

public record EmployeeCreatedEvent(Employee Employee) : IDomainEvent;
```

### 2.2 命名慣例

```
{Aggregate}{Action}Event
```

範例：
- `EmployeeCreatedEvent` - 員工建立
- `EmployeeUpdatedEvent` - 員工更新
- `EmployeeDeactivatedEvent` - 員工停用
- `OrderPlacedEvent` - 訂單建立
- `OrderShippedEvent` - 訂單出貨

### 2.3 事件內容設計

```csharp
// 方法一：包含完整 Entity
public record EmployeeCreatedEvent(Employee Employee) : IDomainEvent;

// 方法二：只包含必要資訊
public record EmployeeCreatedEvent(
    int EmployeeId,
    string Name,
    string Email,
    string Department) : IDomainEvent;

// 方法三：包含變更前後狀態
public record EmployeeStatusChangedEvent(
    int EmployeeId,
    EmployeeStatus OldStatus,
    EmployeeStatus NewStatus) : IDomainEvent;
```

---

## 3. 在 Aggregate 中觸發事件

### 3.1 建立時觸發

```csharp
public class Employee : AggregateRoot<int>
{
    public static ErrorOr<Employee> Create(
        string name,
        string email,
        string department,
        string position,
        DateTime hireDate)
    {
        // ... 驗證邏輯 ...

        var employee = new Employee
        {
            Name = nameResult.Value,
            Email = emailResult.Value,
            Department = departmentResult.Value,
            Position = position,
            HireDate = hireDate,
            Status = EmployeeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 觸發 Domain Event
        employee.RaiseDomainEvent(new EmployeeCreatedEvent(employee));

        return employee;
    }
}
```

### 3.2 狀態變更時觸發

```csharp
public ErrorOr<Success> Deactivate()
{
    if (Status == EmployeeStatus.Inactive)
    {
        return EmployeeErrors.AlreadyInactive;
    }

    var oldStatus = Status;
    Status = EmployeeStatus.Inactive;
    UpdatedAt = DateTime.UtcNow;

    RaiseDomainEvent(new EmployeeStatusChangedEvent(
        Id, oldStatus, EmployeeStatus.Inactive));

    return Result.Success;
}
```

### 3.3 複雜操作觸發多個事件

```csharp
public ErrorOr<Success> TransferDepartment(
    Department newDepartment,
    int? newSupervisorId)
{
    var oldDepartment = Department;
    var oldSupervisorId = SupervisorId;

    Department = newDepartment;
    SupervisorId = newSupervisorId;
    UpdatedAt = DateTime.UtcNow;

    // 觸發部門變更事件
    RaiseDomainEvent(new EmployeeDepartmentChangedEvent(
        Id, oldDepartment, newDepartment));

    // 如果主管也變了，觸發主管變更事件
    if (oldSupervisorId != newSupervisorId)
    {
        RaiseDomainEvent(new EmployeeSupervisorChangedEvent(
            Id, oldSupervisorId, newSupervisorId));
    }

    return Result.Success;
}
```

---

## 4. 處理 Domain Event

### 4.1 Event Handler

```csharp
using Mediator;

namespace OpenClaw.Application.Employees.EventHandlers;

public class EmployeeCreatedEventHandler(
    ILogger<EmployeeCreatedEventHandler> logger)
    : INotificationHandler<EmployeeCreatedEvent>
{
    public ValueTask Handle(
        EmployeeCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        var employee = @event.Employee;

        logger.LogInformation(
            "Employee created: {EmployeeId} - {EmployeeName}",
            employee.Id,
            employee.Name.Value);

        // 這裡可以：
        // - 發送歡迎郵件
        // - 更新搜尋索引
        // - 發布到 Message Queue
        // - 通知其他服務

        return ValueTask.CompletedTask;
    }
}
```

### 4.2 多個 Handler 處理同一事件

```csharp
// Handler 1: 發送通知
public class SendWelcomeEmailOnEmployeeCreated(IEmailService emailService)
    : INotificationHandler<EmployeeCreatedEvent>
{
    public async ValueTask Handle(
        EmployeeCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        await emailService.SendWelcomeEmailAsync(
            @event.Employee.Email.Value,
            @event.Employee.Name.Value,
            cancellationToken);
    }
}

// Handler 2: 發布到 NATS
public class PublishEmployeeCreatedToNats(INatsClient natsClient)
    : INotificationHandler<EmployeeCreatedEvent>
{
    public async ValueTask Handle(
        EmployeeCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        var natsEvent = new CreateEmployeeNatsEvent(
            @event.Employee.Name.Value,
            @event.Employee.Email.Value,
            @event.Employee.Department.ToString(),
            @event.Employee.Position);

        await natsClient.PublishAsync(
            EmployeeNatsSubjects.Created,
            natsEvent,
            cancellationToken);
    }
}

// Handler 3: 更新統計
public class UpdateEmployeeStatsOnCreated(IStatsService statsService)
    : INotificationHandler<EmployeeCreatedEvent>
{
    public async ValueTask Handle(
        EmployeeCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        await statsService.IncrementEmployeeCountAsync(
            @event.Employee.Department.Value,
            cancellationToken);
    }
}
```

---

## 5. 事件發布機制

### 5.1 在 DbContext 中發布

```csharp
public abstract class WedaDbContext : DbContext
{
    private readonly IMediator _mediator;

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // 儲存前提取 Domain Events
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        // 執行儲存
        var result = await base.SaveChangesAsync(cancellationToken);

        // 儲存成功後發布事件
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
```

### 5.2 事件發布時機

| 時機 | 優點 | 缺點 |
|------|------|------|
| 儲存後發布 | 確保資料一致性 | 事件處理失敗不影響主操作 |
| 儲存前發布 | 可在事件處理中拒絕操作 | 可能導致不一致 |

---

## 6. NATS 整合

### 6.1 NATS Event 定義

```csharp
namespace OpenClaw.Contracts.Employees.Events;

public record CreateEmployeeNatsEvent(
    string Name,
    string Email,
    string Department,
    string Position);

public static class EmployeeNatsSubjects
{
    public const string Prefix = "employees";

    public static string BuildCreatedEventSubject(int employeeId, string action)
        => $"{Prefix}.{employeeId}.{action}";

    public const string Created = "employees.created";
    public const string Updated = "employees.updated";
    public const string Deleted = "employees.deleted";
}
```

### 6.2 發布到 NATS

```csharp
public class EmployeeCreatedEventHandler(
    ILogger<EmployeeCreatedEventHandler> logger,
    IJetStreamClient jetStreamClient)
    : INotificationHandler<EmployeeCreatedEvent>
{
    public async ValueTask Handle(
        EmployeeCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        var employee = @event.Employee;
        var subject = EmployeeNatsSubjects.Created;

        var natsEvent = new CreateEmployeeNatsEvent(
            employee.Name.Value,
            employee.Email.Value,
            employee.Department.ToString(),
            employee.Position);

        try
        {
            await jetStreamClient.PublishAsync(subject, natsEvent);

            logger.LogInformation(
                "Published EmployeeCreated event to {Subject}",
                subject);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to publish EmployeeCreated event. NATS may not be available.");
        }
    }
}
```

---

## 快速參考

### 事件處理流程

```
Entity.RaiseDomainEvent()
       ↓
AggregateRoot._domainEvents.Add()
       ↓
DbContext.SaveChangesAsync()
       ↓
PopDomainEvents()
       ↓
Mediator.Publish()
       ↓
INotificationHandler<TEvent>.Handle()
```

### 檔案組織

```
Domain/
└── Employees/
    └── Events/
        ├── EmployeeCreatedEvent.cs
        ├── EmployeeUpdatedEvent.cs
        └── EmployeeStatusChangedEvent.cs

Application/
└── Employees/
    └── EventHandlers/
        ├── EmployeeCreatedEventHandler.cs
        └── EmployeeStatusChangedEventHandler.cs
```

### 設計原則

| 原則 | 說明 |
|------|------|
| 不可變 | Event 應為 record，建立後不可修改 |
| 過去式命名 | 使用過去式表示已發生的事實 |
| 包含必要資訊 | Event 應包含處理所需的所有資訊 |
| Handler 冪等 | Handler 應能安全地重複執行 |

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [01-domain-layer.md](01-domain-layer.md) - Domain Layer（Entity 與 Aggregate）
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer（DbContext）
- [Mediator Documentation](https://github.com/martinothamar/Mediator)
