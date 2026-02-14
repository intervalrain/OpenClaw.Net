---
title: Domain Layer Implementation Guide
description: Step-by-step guide to implementing Domain Layer components in Clean Architecture
keywords: [Domain Layer, DDD, Entity, Value Object, Aggregate Root, Domain Event]
sidebar_position: 2
---

# Domain Layer Implementation Guide

> Learn how to build Domain Layer components following DDD principles

## Overview

The Domain Layer is the heart of Clean Architecture. It contains business logic and domain models with zero external dependencies. All DDD base classes (Entity, AggregateRoot, IDomainEvent, IRepository) are provided by **Weda.Core**.

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

> See [00-weda-core-overview.md](00-weda-core-overview.md) for details on the base classes provided by Weda.Core.

---

## 1. Entity and Aggregate Root

### 1.1 Base Classes (from Weda.Core)

The Domain Layer inherits from `Weda.Core` base classes. You do not need to create these files in your Domain project:

```csharp
// Entity<TId> - Base class for all entities
public abstract class Entity<TId> : IEquatable<Entity<TId>>
{
    public TId Id { get; protected set; }

    // Equality based on Id
    public override bool Equals(object? obj) => ...
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}

// AggregateRoot<TId> - Entity that serves as aggregate boundary
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public List<IDomainEvent> PopDomainEvents() { ... }
    protected void RaiseDomainEvent(IDomainEvent domainEvent) { ... }
}
```

### 1.2 Implementing an Aggregate Root

```csharp
public class Employee : AggregateRoot<int>
{
    // Properties with private setters for encapsulation
    public EmployeeName Name { get; private set; }
    public Email Email { get; private set; }
    public Department Department { get; private set; }
    public string Position { get; private set; }
    public DateTime HireDate { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public int? SupervisorId { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }

    // Private constructor - use Factory Method instead
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

        // Raise domain event
        employee.RaiseDomainEvent(new EmployeeCreatedEvent(employee));

        return employee;
    }

    // State change methods return ErrorOr<Success>
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

### Key Principles

| Principle | Implementation |
|-----------|----------------|
| Encapsulation | Private setters, public methods for state changes |
| Factory Method | `Create()` returns `ErrorOr<T>` |
| Immutable Creation | `private init` for CreatedAt |
| Domain Events | `RaiseDomainEvent()` in state changes |
| Error Handling | Return `ErrorOr<Success>` not exceptions |

---

## 2. Value Objects

Value Objects are immutable, self-validating, and compared by value.

### 2.1 Basic Structure

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

    // Equality by value
    public bool Equals(Email? other) => other?.Value == Value;
    public override bool Equals(object? obj) => obj is Email other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    // Implicit conversion for convenience
    public override string ToString() => Value;
    public static implicit operator string(Email email) => email.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex GenerateEmailRegex();
}
```

### 2.2 Simple Value Object

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

### Value Object Checklist

- [ ] Private constructor
- [ ] Static `Create()` method returning `ErrorOr<T>`
- [ ] Validation in `Create()` method
- [ ] Immutable (readonly properties)
- [ ] Implements `IEquatable<T>`
- [ ] Overrides `Equals()` and `GetHashCode()`
- [ ] Optional: implicit conversion operator

---

## 3. Domain Errors

Define errors as static readonly fields using ErrorOr library.

### 3.1 Error Categories

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

    // Business Rules
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

### Error Types Reference

| Type | HTTP Status | Use Case |
|------|-------------|----------|
| `Error.Validation` | 400 | Input validation failures |
| `Error.NotFound` | 404 | Resource does not exist |
| `Error.Conflict` | 409 | Duplicate or state conflict |
| `Error.Failure` | 500 | Business rule violations |
| `Error.Unauthorized` | 401 | Authentication required |
| `Error.Forbidden` | 403 | Permission denied |

---

## 4. Domain Events

Domain Events capture significant occurrences within the domain.

### 4.1 Defining Events

```csharp
// Base interface from Weda.Core
public interface IDomainEvent : INotification
{
    DateTime OccurredAt { get; }
}

// Domain event as record
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

### 4.2 Raising Events

```csharp
public class Employee : AggregateRoot<int>
{
    public static ErrorOr<Employee> Create(...)
    {
        var employee = new Employee { ... };

        // Raise event on creation
        employee.RaiseDomainEvent(new EmployeeCreatedEvent(employee));

        return employee;
    }

    public ErrorOr<Success> Deactivate()
    {
        var oldStatus = Status;
        Status = EmployeeStatus.Inactive;

        // Raise event on status change
        RaiseDomainEvent(new EmployeeStatusChangedEvent(this, oldStatus, Status));

        return Result.Success;
    }
}
```

### Event Flow

```
Entity State Change
       ↓
RaiseDomainEvent()
       ↓
Event stored in _domainEvents list
       ↓
DbContext.SaveChanges()
       ↓
PopDomainEvents() extracts events
       ↓
Mediator publishes to INotificationHandlers
```

---

## 5. Domain Services

Use Domain Services for logic that spans multiple aggregates or requires external data.

### 5.1 Implementation

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
        // Cannot be own supervisor
        if (employee.Id == supervisorId)
            return EmployeeErrors.CannotBeSelfSupervisor;

        // Check supervisor exists
        var supervisor = await _employeeRepository
            .GetByIdAsync(supervisorId, cancellationToken);

        if (supervisor is null)
            return EmployeeErrors.SupervisorNotFound;

        // Check for circular reference
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

### When to Use Domain Services

| Scenario | Use |
|----------|-----|
| Logic involves single aggregate | Entity method |
| Logic spans multiple aggregates | Domain Service |
| Requires repository access | Domain Service |
| Complex validation with external data | Domain Service |

---

## 6. Repository Interfaces

Define repository interfaces in Domain Layer, implement in Infrastructure.

### 6.1 Base Interface (from Weda.Core)

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

### 6.2 Specialized Interface

```csharp
public interface IEmployeeRepository : IRepository<Employee, int>
{
    Task<Employee?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<List<Employee>> GetBySupervisorIdAsync(int supervisorId, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

---

## Quick Reference

### File Naming Conventions

| Component | Pattern | Example |
|-----------|---------|---------|
| Entity | `{Name}.cs` | `Employee.cs` |
| Value Object | `{Name}.cs` | `Email.cs` |
| Errors | `{Aggregate}Errors.cs` | `EmployeeErrors.cs` |
| Events | `{Aggregate}{Action}Event.cs` | `EmployeeCreatedEvent.cs` |
| Domain Service | `{Name}Manager.cs` or `{Name}Service.cs` | `EmployeeHierarchyManager.cs` |
| Repository Interface | `I{Aggregate}Repository.cs` | `IEmployeeRepository.cs` |

### Folder Structure

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

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [02-application-layer.md](02-application-layer.md) - Application Layer Guide
- [ErrorOr Library](https://github.com/amantinband/error-or)
