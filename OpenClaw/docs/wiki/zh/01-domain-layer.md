---
title: Domain Layer 實作指南
description: Clean Architecture 中 Domain Layer 元件的逐步實作指南
keywords: [Domain Layer, DDD, Entity, Value Object, Aggregate Root, Domain Event]
sidebar_position: 2
---

# Domain Layer 實作指南

> 學習如何依循 DDD 原則建構 Domain Layer 元件

## 概觀

Domain Layer 是 Clean Architecture 的核心，包含業務邏輯與領域模型，且不依賴任何外部套件。所有 DDD Base Class（Entity、AggregateRoot、IDomainEvent、IRepository）皆由 **Weda.Core** 提供。

```
src/OpenClaw.Domain/
└── Employees/
    ├── Entities/
    │   └── Employee.cs
    ├── ValueObjects/
    │   ├── Email.cs
    │   ├── EmployeeName.cs
    │   └── Department.cs
    ├── Enums/
    │   └── EmployeeStatus.cs
    ├── Errors/
    │   └── EmployeeErrors.cs
    ├── Events/
    │   └── EmployeeCreatedEvent.cs
    ├── DomainServices/
    │   └── EmployeeHierarchyManager.cs
    └── Repositories/
        └── IEmployeeRepository.cs
```

> 請參閱 [00-weda-core-overview.md](00-weda-core-overview.md) 了解 Weda.Core 提供的 Base Class 詳細資訊。

---

## 1. Entity 與 Aggregate Root

### 1.1 Base Class（來自 Weda.Core）

Domain Layer 繼承自 `Weda.Core` 的 Base Class。您不需要在 Domain 專案中建立這些檔案：

```csharp
// Entity<TId> - 所有 Entity 的 Base Class
public abstract class Entity<TId> : IEquatable<Entity<TId>>
{
    public TId Id { get; protected set; }

    // 基於 Id 的相等性比較
    public override bool Equals(object? obj) => ...
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}

// AggregateRoot<TId> - 作為 Aggregate 邊界的 Entity
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public List<IDomainEvent> PopDomainEvents() { ... }
    protected void RaiseDomainEvent(IDomainEvent domainEvent) { ... }
}
```

### 1.2 實作 Aggregate Root

```csharp
public class Employee : AggregateRoot<int>
{
    // 使用 Private Setter 進行封裝
    public EmployeeName Name { get; private set; }
    public Email Email { get; private set; }
    public Department Department { get; private set; }
    public string Position { get; private set; }
    public DateTime HireDate { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public int? SupervisorId { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }

    // Private Constructor - 改用 Factory Method
    private Employee() { }

    // Factory Method Pattern
    public static ErrorOr<Employee> Create(
        EmployeeName name,
        Email email,
        Department department,
        string position,
        DateTime hireDate,
        int? supervisorId = null)
    {
        var employee = new Employee
        {
            Name = name,
            Email = email,
            Department = department,
            Position = position,
            HireDate = hireDate,
            SupervisorId = supervisorId,
            Status = EmployeeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 發送 Domain Event
        employee.RaiseDomainEvent(new EmployeeCreatedEvent(employee));

        return employee;
    }

    // 狀態變更方法回傳 ErrorOr<Success>
    public ErrorOr<Success> UpdateInfo(
        EmployeeName name,
        Department department,
        string position)
    {
        if (Status == EmployeeStatus.Inactive)
            return EmployeeErrors.CannotModifyInactive;

        Name = name;
        Department = department;
        Position = position;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success;
    }

    public ErrorOr<Success> Deactivate()
    {
        if (Status == EmployeeStatus.Inactive)
            return EmployeeErrors.AlreadyInactive;

        Status = EmployeeStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success;
    }
}
```

### 核心原則

| 原則 | 實作方式 |
|------|----------|
| Encapsulation | Private Setter，透過 Public Method 變更狀態 |
| Factory Method | `Create()` 回傳 `ErrorOr<T>` |
| Immutable Creation | `private init` 用於 CreatedAt |
| Domain Event | 在狀態變更時呼叫 `RaiseDomainEvent()` |
| Error Handling | 回傳 `ErrorOr<Success>` 而非拋出 Exception |

---

## 2. Value Object

Value Object 具有 Immutability、自我驗證、以及 Value Equality 特性。

### 2.1 基本結構

```csharp
public sealed partial class Email : IEquatable<Email>
{
    public const int MaxLength = 256;
    private static readonly Regex EmailRegex = GenerateEmailRegex();

    public string Value { get; }

    private Email(string value) => Value = value;

    public static ErrorOr<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EmployeeErrors.EmailRequired;

        if (value.Length > MaxLength)
            return EmployeeErrors.EmailTooLong;

        if (!EmailRegex.IsMatch(value))
            return EmployeeErrors.InvalidEmailFormat;

        return new Email(value);
    }

    // Value Equality
    public bool Equals(Email? other) => other?.Value == Value;
    public override bool Equals(object? obj) => obj is Email other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    // 便利的隱式轉換
    public override string ToString() => Value;
    public static implicit operator string(Email email) => email.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex GenerateEmailRegex();
}
```

### 2.2 簡單的 Value Object

```csharp
public sealed class EmployeeName : IEquatable<EmployeeName>
{
    public const int MaxLength = 100;

    public string Value { get; }

    private EmployeeName(string value) => Value = value;

    public static ErrorOr<EmployeeName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EmployeeErrors.NameRequired;

        if (value.Length > MaxLength)
            return EmployeeErrors.NameTooLong;

        return new EmployeeName(value);
    }

    public bool Equals(EmployeeName? other) => other?.Value == Value;
    public override bool Equals(object? obj) => obj is EmployeeName other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
    public static implicit operator string(EmployeeName name) => name.Value;
}
```

### Value Object 檢查清單

- [ ] Private Constructor
- [ ] Static `Create()` 方法回傳 `ErrorOr<T>`
- [ ] 在 `Create()` 方法中進行驗證
- [ ] Immutable（唯讀屬性）
- [ ] 實作 `IEquatable<T>`
- [ ] 覆寫 `Equals()` 與 `GetHashCode()`
- [ ] 選用：隱式轉換運算子

---

## 3. Domain Error

使用 ErrorOr Library 將 Error 定義為 Static Readonly Field。

### 3.1 Error 分類

```csharp
public static class EmployeeErrors
{
    // Not Found (404)
    public static readonly Error NotFound = Error.NotFound(
        code: "Employee.NotFound",
        description: "Employee not found");

    // Validation (400)
    public static readonly Error NameRequired = Error.Validation(
        code: "Employee.NameRequired",
        description: "Employee name is required");

    public static readonly Error NameTooLong = Error.Validation(
        code: "Employee.NameTooLong",
        description: $"Employee name cannot exceed {EmployeeName.MaxLength} characters");

    public static readonly Error EmailRequired = Error.Validation(
        code: "Employee.EmailRequired",
        description: "Email is required");

    public static readonly Error InvalidEmailFormat = Error.Validation(
        code: "Employee.InvalidEmailFormat",
        description: "Invalid email format");

    // Business Rule
    public static readonly Error CannotModifyInactive = Error.Failure(
        code: "Employee.CannotModifyInactive",
        description: "Cannot modify inactive employee");

    public static readonly Error CannotBeSelfSupervisor = Error.Validation(
        code: "Employee.CannotBeSelfSupervisor",
        description: "Employee cannot be their own supervisor");

    // Conflict (409)
    public static readonly Error DuplicateEmail = Error.Conflict(
        code: "Employee.DuplicateEmail",
        description: "An employee with this email already exists");

    public static readonly Error AlreadyInactive = Error.Conflict(
        code: "Employee.AlreadyInactive",
        description: "Employee is already inactive");
}
```

### Error 類型參考

| 類型 | HTTP Status | 使用情境 |
|------|-------------|----------|
| `Error.Validation` | 400 | 輸入驗證失敗 |
| `Error.NotFound` | 404 | 資源不存在 |
| `Error.Conflict` | 409 | 重複或狀態衝突 |
| `Error.Failure` | 500 | 業務規則違反 |
| `Error.Unauthorized` | 401 | 需要驗證 |
| `Error.Forbidden` | 403 | 權限不足 |

---

## 4. Domain Event

Domain Event 用於捕捉領域中的重要事件。

### 4.1 定義 Event

```csharp
// Weda.Core 的 Base Interface
public interface IDomainEvent : INotification
{
    DateTime OccurredAt { get; }
}

// 使用 Record 定義 Domain Event
public record EmployeeCreatedEvent(Employee Employee) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record EmployeeStatusChangedEvent(
    Employee Employee,
    EmployeeStatus OldStatus,
    EmployeeStatus NewStatus) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

### 4.2 發送 Event

```csharp
public class Employee : AggregateRoot<int>
{
    public static ErrorOr<Employee> Create(...)
    {
        var employee = new Employee { ... };

        // 建立時發送 Event
        employee.RaiseDomainEvent(new EmployeeCreatedEvent(employee));

        return employee;
    }

    public ErrorOr<Success> Deactivate()
    {
        var oldStatus = Status;
        Status = EmployeeStatus.Inactive;

        // 狀態變更時發送 Event
        RaiseDomainEvent(new EmployeeStatusChangedEvent(this, oldStatus, Status));

        return Result.Success;
    }
}
```

### Event 流程

```
Entity State Change
       ↓
RaiseDomainEvent()
       ↓
Event 儲存於 _domainEvents List
       ↓
DbContext.SaveChanges()
       ↓
PopDomainEvents() 提取 Event
       ↓
Mediator 發布至 INotificationHandler
```

---

## 5. Domain Service

當邏輯跨越多個 Aggregate 或需要外部資料時，使用 Domain Service。

### 5.1 實作

```csharp
public class EmployeeHierarchyManager
{
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeeHierarchyManager(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<ErrorOr<Success>> AssignSupervisorAsync(
        Employee employee,
        int supervisorId,
        CancellationToken cancellationToken = default)
    {
        // 不能是自己的主管
        if (employee.Id == supervisorId)
            return EmployeeErrors.CannotBeSelfSupervisor;

        // 檢查主管是否存在
        var supervisor = await _employeeRepository
            .GetByIdAsync(supervisorId, cancellationToken);

        if (supervisor is null)
            return EmployeeErrors.SupervisorNotFound;

        // 檢查是否有循環參照
        if (await HasCircularReferenceAsync(employee.Id, supervisorId, cancellationToken))
            return EmployeeErrors.CircularSupervisorReference;

        return employee.AssignSupervisor(supervisorId);
    }

    private async Task<bool> HasCircularReferenceAsync(
        int employeeId,
        int supervisorId,
        CancellationToken cancellationToken)
    {
        var currentId = supervisorId;
        var visited = new HashSet<int>();

        while (currentId.HasValue)
        {
            if (currentId == employeeId)
                return true;

            if (!visited.Add(currentId.Value))
                break;

            var current = await _employeeRepository
                .GetByIdAsync(currentId.Value, cancellationToken);
            currentId = current?.SupervisorId;
        }

        return false;
    }
}
```

### 何時使用 Domain Service

| 情境 | 使用 |
|------|------|
| 邏輯只涉及單一 Aggregate | Entity Method |
| 邏輯跨越多個 Aggregate | Domain Service |
| 需要 Repository 存取 | Domain Service |
| 需要外部資料的複雜驗證 | Domain Service |

---

## 6. Repository Interface

在 Domain Layer 定義 Repository Interface，在 Infrastructure 實作。

### 6.1 Base Interface（來自 Weda.Core）

```csharp
public interface IRepository<T, TId>
{
    Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}
```

### 6.2 特化的 Interface

```csharp
public interface IEmployeeRepository : IRepository<Employee, int>
{
    Task<Employee?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<List<Employee>> GetBySupervisorIdAsync(int supervisorId, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

---

## 快速參考

### 檔案命名慣例

| 元件 | 模式 | 範例 |
|------|------|------|
| Entity | `{Name}.cs` | `Employee.cs` |
| Value Object | `{Name}.cs` | `Email.cs` |
| Error | `{Aggregate}Errors.cs` | `EmployeeErrors.cs` |
| Event | `{Aggregate}{Action}Event.cs` | `EmployeeCreatedEvent.cs` |
| Domain Service | `{Name}Manager.cs` 或 `{Name}Service.cs` | `EmployeeHierarchyManager.cs` |
| Repository Interface | `I{Aggregate}Repository.cs` | `IEmployeeRepository.cs` |

### 資料夾結構

```
Domain/
└── {AggregateName}/
    ├── Entities/
    ├── ValueObjects/
    ├── Enums/
    ├── Errors/
    ├── Events/
    ├── DomainServices/
    └── Repositories/
```

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [02-application-layer.md](02-application-layer.md) - Application Layer 指南
- [ErrorOr Library](https://github.com/amantinband/error-or)
