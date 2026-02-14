---
title: Application Layer Implementation Guide
description: Step-by-step guide to implementing Application Layer with CQRS pattern
keywords: [Application Layer, CQRS, Command, Query, Mediator, Handler]
sidebar_position: 3
---

# Application Layer Implementation Guide

> Learn how to implement use cases with CQRS pattern and Mediator

## Overview

The Application Layer orchestrates domain operations and implements use cases. It depends on Domain Layer but knows nothing about Infrastructure or API layers.

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

## 1. Commands and Queries (CQRS)

### 1.1 Command Definition

Commands are defined in the Contracts layer and handled in Application layer.

```csharp
// In Contracts layer: OpenClaw.Contracts/Employees/Commands/
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

### 1.2 Query Definition

```csharp
// In Contracts layer: OpenClaw.Contracts/Employees/Queries/
public record GetEmployeeQuery(int Id) : IRequest<ErrorOr<EmployeeDto>>;

public record ListEmployeesQuery() : IRequest<ErrorOr<List<EmployeeDto>>>;

public record GetSubordinatesQuery(
    int EmployeeId,
    bool IncludeIndirect = false) : IRequest<ErrorOr<List<EmployeeDto>>>;
```

### Command vs Query

| Aspect | Command | Query |
|--------|---------|-------|
| Purpose | Change state | Read data |
| Return Type | `ErrorOr<T>` or `ErrorOr<Deleted>` | `ErrorOr<TDto>` or `ErrorOr<List<TDto>>` |
| Side Effects | Yes | No |
| Folder | `Commands/{ActionName}/` | `Queries/{ActionName}/` |

---

## 2. Command Handlers

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
        // 1. Create Value Objects
        var nameResult = EmployeeName.Create(request.Name);
        if (nameResult.IsError)
            return nameResult.Errors;

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsError)
            return emailResult.Errors;

        var departmentResult = Department.Create(request.Department);
        if (departmentResult.IsError)
            return departmentResult.Errors;

        // 2. Check for duplicate email
        if (await _employeeRepository.ExistsWithEmailAsync(
            emailResult.Value, cancellationToken))
        {
            return EmployeeErrors.DuplicateEmail;
        }

        // 3. Create Aggregate
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

        // 4. Validate supervisor if provided
        if (request.SupervisorId.HasValue)
        {
            var assignResult = await _hierarchyManager.AssignSupervisorAsync(
                employee,
                request.SupervisorId.Value,
                cancellationToken);

            if (assignResult.IsError)
                return assignResult.Errors;
        }

        // 5. Persist
        await _employeeRepository.AddAsync(employee, cancellationToken);

        // 6. Map and return
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
        // 1. Retrieve entity
        var employee = await _employeeRepository
            .GetByIdAsync(request.Id, cancellationToken);

        if (employee is null)
            return EmployeeErrors.NotFound;

        // 2. Create Value Objects
        var nameResult = EmployeeName.Create(request.Name);
        if (nameResult.IsError)
            return nameResult.Errors;

        var departmentResult = Department.Create(request.Department);
        if (departmentResult.IsError)
            return departmentResult.Errors;

        // 3. Update via domain method
        var updateResult = employee.UpdateInfo(
            nameResult.Value,
            departmentResult.Value,
            request.Position);

        if (updateResult.IsError)
            return updateResult.Errors;

        // 4. Persist
        await _employeeRepository.UpdateAsync(employee, cancellationToken);

        // 5. Return mapped DTO
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

        // Check business rules before delete
        var subordinates = await _employeeRepository
            .GetBySupervisorIdAsync(request.Id, cancellationToken);

        if (subordinates.Count > 0)
            return EmployeeErrors.HasSubordinates;

        await _employeeRepository.DeleteAsync(employee, cancellationToken);

        return Result.Deleted;
    }
}
```

### Handler Pattern

```
┌────────────────────────────────────────────────────────────────────┐
│                        Command Handler Flow                        │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  1. Receive Command                                                │
│         ↓                                                          │
│  2. Create/Validate Value Objects                                  │
│         ↓                                                          │
│  3. Retrieve Entity (if update/delete)                             │
│         ↓                                                          │
│  4. Execute Domain Logic                                           │
│         ↓                                                          │
│  5. Use Domain Service (if needed)                                 │
│         ↓                                                          │
│  6. Persist via Repository                                         │
│         ↓                                                          │
│  7. Map to DTO and Return                                          │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## 3. Query Handlers

### 3.1 Get Single Entity

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

### 3.2 List Entities

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

### 3.3 Complex Query with Domain Service

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

## 4. Validators (FluentValidation)

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

The `ValidationBehavior` from Weda.Core automatically intercepts requests:

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

## 5. Event Handlers

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

        // Publish to NATS
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

### Event Handler Best Practices

| Practice | Description |
|----------|-------------|
| Idempotency | Handler should be safe to run multiple times |
| Async Operations | Use for external integrations (NATS, email, etc.) |
| Error Handling | Log errors, consider retry mechanisms |
| Single Responsibility | One handler per side effect |

---

## 6. Mappers

### 6.1 Mapperly Source Generator

```csharp
[Mapper]
public static partial class EmployeeMapper
{
    public static partial EmployeeDto ToDto(Employee employee);

    // Custom mapping for Value Objects
    private static string MapName(EmployeeName name) => name.Value;
    private static string MapEmail(Email email) => email.Value;
    private static string MapDepartment(Department dept) => dept.Value;
    private static string MapStatus(EmployeeStatus status) => status.ToString();
}
```

### 6.2 DTO Definition

```csharp
// In Contracts layer
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

## 7. Module Registration

### 7.1 Application Module

```csharp
public static class WedaTemplateApplicationModule
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // Register application-specific services here
        // Mediator and validators are registered in Weda.Core

        return services;
    }
}
```

### 7.2 Assembly Marker

```csharp
// Used for assembly scanning
public interface IApplicationMarker { }
```

---

## Quick Reference

### File Naming Conventions

| Component | Pattern | Example |
|-----------|---------|---------|
| Command | `{Action}{Aggregate}Command.cs` | `CreateEmployeeCommand.cs` |
| Command Handler | `{Action}{Aggregate}CommandHandler.cs` | `CreateEmployeeCommandHandler.cs` |
| Validator | `{Command}Validator.cs` | `CreateEmployeeCommandValidator.cs` |
| Query | `{Action}{Aggregate}Query.cs` | `GetEmployeeQuery.cs` |
| Query Handler | `{Action}{Aggregate}QueryHandler.cs` | `GetEmployeeQueryHandler.cs` |
| Event Handler | `{Event}Handler.cs` | `EmployeeCreatedEventHandler.cs` |
| Mapper | `{Aggregate}Mapper.cs` | `EmployeeMapper.cs` |

### Folder Structure

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

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [01-domain-layer.md](01-domain-layer.md) - Domain Layer Guide
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer Guide
- [Mediator Documentation](https://github.com/martinothamar/Mediator)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
