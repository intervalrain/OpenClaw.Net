---
title: Application Layer 實作指南
description: 使用 CQRS Pattern 與 Mediator 實作 Application Layer 的逐步指南
keywords: [Application Layer, CQRS, Command, Query, Mediator, Handler]
sidebar_position: 3
---

# Application Layer 實作指南

> 學習如何使用 CQRS Pattern 與 Mediator 實作 Use Case

## 概觀

Application Layer 負責協調 Domain 操作並實作 Use Case。它依賴 Domain Layer，但不知道 Infrastructure 或 API Layer 的存在。

```
src/OpenClaw.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── ValidationBehavior.cs
│   │   └── AuthorizationBehavior.cs
│   └── Interfaces/
│       └── IDateTimeProvider.cs
├── Employees/
│   ├── Commands/
│   │   ├── CreateEmployee/
│   │   │   ├── CreateEmployeeCommandHandler.cs
│   │   │   └── CreateEmployeeCommandValidator.cs
│   │   ├── UpdateEmployee/
│   │   └── DeleteEmployee/
│   ├── Queries/
│   │   ├── GetEmployee/
│   │   ├── ListEmployees/
│   │   └── GetSubordinates/
│   ├── EventHandlers/
│   │   └── EmployeeCreatedEventHandler.cs
│   └── Mapping/
│       └── EmployeeMapper.cs
├── WedaTemplateApplicationModule.cs
└── IApplicationMarker.cs
```

---

## 1. Command 與 Query (CQRS)

### 1.1 Command 定義

Command 在 Contracts Layer 定義，在 Application Layer 處理。

```csharp
// 在 Contracts Layer: OpenClaw.Contracts/Employees/Commands/
public record CreateEmployeeCommand(
    string Name,
    string Email,
    string Department,
    string Position,
    DateTime HireDate,
    int? SupervisorId = null) : IRequest<ErrorOr<EmployeeDto>>;

public record UpdateEmployeeCommand(
    int Id,
    string Name,
    string Department,
    string Position) : IRequest<ErrorOr<EmployeeDto>>;

public record DeleteEmployeeCommand(int Id) : IRequest<ErrorOr<Deleted>>;
```

### 1.2 Query 定義

```csharp
// 在 Contracts Layer: OpenClaw.Contracts/Employees/Queries/
public record GetEmployeeQuery(int Id) : IRequest<ErrorOr<EmployeeDto>>;

public record ListEmployeesQuery() : IRequest<ErrorOr<List<EmployeeDto>>>;

public record GetSubordinatesQuery(
    int EmployeeId,
    bool IncludeIndirect = false) : IRequest<ErrorOr<List<EmployeeDto>>>;
```

### Command vs Query

| 面向 | Command | Query |
|------|---------|-------|
| 目的 | 變更狀態 | 讀取資料 |
| 回傳類型 | `ErrorOr<T>` 或 `ErrorOr<Deleted>` | `ErrorOr<TDto>` 或 `ErrorOr<List<TDto>>` |
| Side Effect | 有 | 無 |
| 資料夾 | `Commands/{ActionName}/` | `Queries/{ActionName}/` |

---

## 2. Command Handler

### 2.1 Create Handler

```csharp
public class CreateEmployeeCommandHandler
    : IRequestHandler<CreateEmployeeCommand, ErrorOr<EmployeeDto>>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly EmployeeHierarchyManager _hierarchyManager;

    public CreateEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        EmployeeHierarchyManager hierarchyManager)
    {
        _employeeRepository = employeeRepository;
        _hierarchyManager = hierarchyManager;
    }

    public async Task<ErrorOr<EmployeeDto>> Handle(
        CreateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        // 1. 建立 Value Object
        var nameResult = EmployeeName.Create(request.Name);
        if (nameResult.IsError)
            return nameResult.Errors;

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsError)
            return emailResult.Errors;

        var departmentResult = Department.Create(request.Department);
        if (departmentResult.IsError)
            return departmentResult.Errors;

        // 2. 檢查 Email 是否重複
        if (await _employeeRepository.ExistsWithEmailAsync(
            emailResult.Value, cancellationToken))
        {
            return EmployeeErrors.DuplicateEmail;
        }

        // 3. 建立 Aggregate
        var employeeResult = Employee.Create(
            nameResult.Value,
            emailResult.Value,
            departmentResult.Value,
            request.Position,
            request.HireDate,
            request.SupervisorId);

        if (employeeResult.IsError)
            return employeeResult.Errors;

        var employee = employeeResult.Value;

        // 4. 驗證 Supervisor（若有提供）
        if (request.SupervisorId.HasValue)
        {
            var assignResult = await _hierarchyManager.AssignSupervisorAsync(
                employee,
                request.SupervisorId.Value,
                cancellationToken);

            if (assignResult.IsError)
                return assignResult.Errors;
        }

        // 5. 持久化
        await _employeeRepository.AddAsync(employee, cancellationToken);

        // 6. 轉換並回傳
        return EmployeeMapper.ToDto(employee);
    }
}
```

### 2.2 Update Handler

```csharp
public class UpdateEmployeeCommandHandler
    : IRequestHandler<UpdateEmployeeCommand, ErrorOr<EmployeeDto>>
{
    private readonly IEmployeeRepository _employeeRepository;

    public UpdateEmployeeCommandHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<ErrorOr<EmployeeDto>> Handle(
        UpdateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        // 1. 取得 Entity
        var employee = await _employeeRepository
            .GetByIdAsync(request.Id, cancellationToken);

        if (employee is null)
            return EmployeeErrors.NotFound;

        // 2. 建立 Value Object
        var nameResult = EmployeeName.Create(request.Name);
        if (nameResult.IsError)
            return nameResult.Errors;

        var departmentResult = Department.Create(request.Department);
        if (departmentResult.IsError)
            return departmentResult.Errors;

        // 3. 透過 Domain Method 更新
        var updateResult = employee.UpdateInfo(
            nameResult.Value,
            departmentResult.Value,
            request.Position);

        if (updateResult.IsError)
            return updateResult.Errors;

        // 4. 持久化
        await _employeeRepository.UpdateAsync(employee, cancellationToken);

        // 5. 回傳轉換後的 DTO
        return EmployeeMapper.ToDto(employee);
    }
}
```

### 2.3 Delete Handler

```csharp
public class DeleteEmployeeCommandHandler
    : IRequestHandler<DeleteEmployeeCommand, ErrorOr<Deleted>>
{
    private readonly IEmployeeRepository _employeeRepository;

    public DeleteEmployeeCommandHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<ErrorOr<Deleted>> Handle(
        DeleteEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository
            .GetByIdAsync(request.Id, cancellationToken);

        if (employee is null)
            return EmployeeErrors.NotFound;

        // 刪除前檢查業務規則
        var subordinates = await _employeeRepository
            .GetBySupervisorIdAsync(request.Id, cancellationToken);

        if (subordinates.Count > 0)
            return EmployeeErrors.HasSubordinates;

        await _employeeRepository.DeleteAsync(employee, cancellationToken);

        return Result.Deleted;
    }
}
```

### Handler 流程

```
┌────────────────────────────────────────────────────────────────────┐
│                        Command Handler Flow                        │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  1. 接收 Command                                                   │
│         ↓                                                          │
│  2. 建立/驗證 Value Object                                         │
│         ↓                                                          │
│  3. 取得 Entity（若為 Update/Delete）                              │
│         ↓                                                          │
│  4. 執行 Domain Logic                                              │
│         ↓                                                          │
│  5. 使用 Domain Service（若需要）                                  │
│         ↓                                                          │
│  6. 透過 Repository 持久化                                         │
│         ↓                                                          │
│  7. 轉換為 DTO 並回傳                                              │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## 3. Query Handler

### 3.1 取得單一 Entity

```csharp
public class GetEmployeeQueryHandler
    : IRequestHandler<GetEmployeeQuery, ErrorOr<EmployeeDto>>
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<ErrorOr<EmployeeDto>> Handle(
        GetEmployeeQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository
            .GetByIdAsync(request.Id, cancellationToken);

        if (employee is null)
            return EmployeeErrors.NotFound;

        return EmployeeMapper.ToDto(employee);
    }
}
```

### 3.2 列出 Entity

```csharp
public class ListEmployeesQueryHandler
    : IRequestHandler<ListEmployeesQuery, ErrorOr<List<EmployeeDto>>>
{
    private readonly IEmployeeRepository _employeeRepository;

    public ListEmployeesQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<ErrorOr<List<EmployeeDto>>> Handle(
        ListEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var employees = await _employeeRepository
            .GetAllAsync(cancellationToken);

        return employees.Select(EmployeeMapper.ToDto).ToList();
    }
}
```

### 3.3 使用 Domain Service 的複雜 Query

```csharp
public class GetSubordinatesQueryHandler
    : IRequestHandler<GetSubordinatesQuery, ErrorOr<List<EmployeeDto>>>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly EmployeeHierarchyManager _hierarchyManager;

    public GetSubordinatesQueryHandler(
        IEmployeeRepository employeeRepository,
        EmployeeHierarchyManager hierarchyManager)
    {
        _employeeRepository = employeeRepository;
        _hierarchyManager = hierarchyManager;
    }

    public async Task<ErrorOr<List<EmployeeDto>>> Handle(
        GetSubordinatesQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository
            .GetByIdAsync(request.EmployeeId, cancellationToken);

        if (employee is null)
            return EmployeeErrors.NotFound;

        var subordinates = request.IncludeIndirect
            ? await _hierarchyManager.GetAllSubordinatesAsync(
                request.EmployeeId, cancellationToken)
            : await _employeeRepository.GetBySupervisorIdAsync(
                request.EmployeeId, cancellationToken);

        return subordinates.Select(EmployeeMapper.ToDto).ToList();
    }
}
```

---

## 4. Validator (FluentValidation)

### 4.1 Command Validator

```csharp
public class CreateEmployeeCommandValidator
    : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(EmployeeName.MaxLength)
            .WithMessage($"Name cannot exceed {EmployeeName.MaxLength} characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(Email.MaxLength)
            .WithMessage($"Email cannot exceed {Email.MaxLength} characters")
            .EmailAddress()
            .WithMessage("Invalid email format");

        RuleFor(x => x.Department)
            .NotEmpty()
            .WithMessage("Department is required")
            .MaximumLength(Department.MaxLength)
            .WithMessage($"Department cannot exceed {Department.MaxLength} characters");

        RuleFor(x => x.Position)
            .NotEmpty()
            .WithMessage("Position is required")
            .MaximumLength(100)
            .WithMessage("Position cannot exceed 100 characters");

        RuleFor(x => x.HireDate)
            .NotEmpty()
            .WithMessage("Hire date is required")
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Hire date cannot be in the future");
    }
}
```

### 4.2 Validation Pipeline Behavior

Weda.Core 的 `ValidationBehavior` 會自動攔截 Request：

```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var errors = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))
            .ToList();

        if (errors.Count > 0)
            return (dynamic)errors;

        return await next();
    }
}
```

---

## 5. Event Handler

### 5.1 Domain Event Handler

```csharp
public class EmployeeCreatedEventHandler
    : INotificationHandler<EmployeeCreatedEvent>
{
    private readonly ILogger<EmployeeCreatedEventHandler> _logger;
    private readonly INatsConnectionProvider _natsProvider;

    public EmployeeCreatedEventHandler(
        ILogger<EmployeeCreatedEventHandler> logger,
        INatsConnectionProvider natsProvider)
    {
        _logger = logger;
        _natsProvider = natsProvider;
    }

    public async Task Handle(
        EmployeeCreatedEvent notification,
        CancellationToken cancellationToken)
    {
        var employee = notification.Employee;

        _logger.LogInformation(
            "Employee created: {Id} - {Name}",
            employee.Id,
            employee.Name);

        // 發布至 NATS
        var natsEvent = new CreateEmployeeNatsEvent(
            employee.Id,
            employee.Name,
            employee.Email,
            employee.Department,
            employee.CreatedAt);

        await _natsProvider.PublishAsync(
            EmployeeNatsSubjects.Created,
            natsEvent,
            cancellationToken);
    }
}
```

### Event Handler 最佳實踐

| 實踐 | 說明 |
|------|------|
| Idempotency | Handler 應該可以安全地執行多次 |
| Async Operation | 用於外部整合（NATS、Email 等） |
| Error Handling | 記錄錯誤，考慮重試機制 |
| Single Responsibility | 每個 Handler 只處理一個 Side Effect |

---

## 6. Mapper

### 6.1 Mapperly Source Generator

```csharp
[Mapper]
public static partial class EmployeeMapper
{
    public static partial EmployeeDto ToDto(Employee employee);

    // Value Object 的自訂 Mapping
    private static string MapName(EmployeeName name) => name.Value;
    private static string MapEmail(Email email) => email.Value;
    private static string MapDepartment(Department dept) => dept.Value;
    private static string MapStatus(EmployeeStatus status) => status.ToString();
}
```

### 6.2 DTO 定義

```csharp
// 在 Contracts Layer
public record EmployeeDto(
    int Id,
    string Name,
    string Email,
    string Department,
    string Position,
    DateTime HireDate,
    string Status,
    int? SupervisorId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

---

## 7. Module 註冊

### 7.1 Application Module

```csharp
public static class WedaTemplateApplicationModule
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // 在此註冊 Application 專屬的 Service
        // Mediator 與 Validator 在 Weda.Core 中註冊

        return services;
    }
}
```

### 7.2 Assembly Marker

```csharp
// 用於 Assembly Scanning
public interface IApplicationMarker { }
```

---

## 快速參考

### 檔案命名慣例

| 元件 | 模式 | 範例 |
|------|------|------|
| Command | `{Action}{Aggregate}Command.cs` | `CreateEmployeeCommand.cs` |
| Command Handler | `{Action}{Aggregate}CommandHandler.cs` | `CreateEmployeeCommandHandler.cs` |
| Validator | `{Command}Validator.cs` | `CreateEmployeeCommandValidator.cs` |
| Query | `{Action}{Aggregate}Query.cs` | `GetEmployeeQuery.cs` |
| Query Handler | `{Action}{Aggregate}QueryHandler.cs` | `GetEmployeeQueryHandler.cs` |
| Event Handler | `{Event}Handler.cs` | `EmployeeCreatedEventHandler.cs` |
| Mapper | `{Aggregate}Mapper.cs` | `EmployeeMapper.cs` |

### 資料夾結構

```
Application/
└── {AggregateName}/
    ├── Commands/
    │   └── {ActionName}/
    │       ├── {Action}CommandHandler.cs
    │       └── {Action}CommandValidator.cs
    ├── Queries/
    │   └── {ActionName}/
    │       └── {Action}QueryHandler.cs
    ├── EventHandlers/
    │   └── {Event}Handler.cs
    └── Mapping/
        └── {Aggregate}Mapper.cs
```

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [01-domain-layer.md](01-domain-layer.md) - Domain Layer 指南
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer 指南
- [Mediator Documentation](https://github.com/martinothamar/Mediator)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
