---
title: Domain Events Guide
description: Implementation guide for Domain Event publishing and handling
keywords: [Domain Events, Event Handlers, Mediator, CQRS]
sidebar_position: 12
---

# Domain Events Guide

> Learn how to implement event-driven architecture using Domain Events

## Overview

Domain Events are a core DDD concept, used for:
- Decoupling dependencies between Aggregates
- Triggering side effects (sending notifications, updating cache, etc.)
- Implementing eventual consistency
- Integrating with external systems (NATS, Message Queue)

```
src/
├── Weda.Core/Domain/
│   ├── IDomainEvent.cs             # Domain Event interface
│   ├── IAggregateRoot.cs           # Aggregate Root interface
│   └── AggregateRoot.cs            # Aggregate Root base class
├── OpenClaw.Domain/
│   └── {Aggregate}/Events/
│       └── {Event}Event.cs         # Domain Event definition
└── OpenClaw.Application/
    └── {Feature}/EventHandlers/
        └── {Event}EventHandler.cs  # Event Handler
```

---

## 1. Domain Event Basics

### 1.1 IDomainEvent Interface

```csharp
using Mediator;

namespace Weda.Core.Domain;

public interface IDomainEvent : INotification
{
}
```

Domain Event inherits from Mediator's `INotification`, allowing publication through Mediator.

### 1.2 AggregateRoot Base Class

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }
    protected AggregateRoot() { }

    // Pop and clear Domain Events
    public List<IDomainEvent> PopDomainEvents()
    {
        var copy = _domainEvents.ToList();
        _domainEvents.Clear();
        return copy;
    }

    // Raise event within Aggregate
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
```

---

## 2. Defining Domain Events

### 2.1 Basic Structure

```csharp
namespace OpenClaw.Domain.Employees.Events;

public record EmployeeCreatedEvent(Employee Employee) : IDomainEvent;
```

### 2.2 Naming Convention

```
{Aggregate}{Action}Event
```

Examples:
- `EmployeeCreatedEvent` - Employee created
- `EmployeeUpdatedEvent` - Employee updated
- `EmployeeDeactivatedEvent` - Employee deactivated
- `OrderPlacedEvent` - Order placed
- `OrderShippedEvent` - Order shipped

### 2.3 Event Content Design

```csharp
// Option 1: Include complete Entity
public record EmployeeCreatedEvent(Employee Employee) : IDomainEvent;

// Option 2: Include only necessary information
public record EmployeeCreatedEvent(
    int EmployeeId,
    string Name,
    string Email,
    string Department) : IDomainEvent;

// Option 3: Include before and after state
public record EmployeeStatusChangedEvent(
    int EmployeeId,
    EmployeeStatus OldStatus,
    EmployeeStatus NewStatus) : IDomainEvent;
```

---

## 3. Raising Events in Aggregates

### 3.1 On Creation

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
        // ... validation logic ...

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

        // Raise Domain Event
        employee.RaiseDomainEvent(new EmployeeCreatedEvent(employee));

        return employee;
    }
}
```

### 3.2 On State Change

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

### 3.3 Complex Operations Raising Multiple Events

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

    // Raise department change event
    RaiseDomainEvent(new EmployeeDepartmentChangedEvent(
        Id, oldDepartment, newDepartment));

    // If supervisor also changed, raise supervisor change event
    if (oldSupervisorId != newSupervisorId)
    {
        RaiseDomainEvent(new EmployeeSupervisorChangedEvent(
            Id, oldSupervisorId, newSupervisorId));
    }

    return Result.Success;
}
```

---

## 4. Handling Domain Events

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

        // Here you can:
        // - Send welcome email
        // - Update search index
        // - Publish to Message Queue
        // - Notify other services

        return ValueTask.CompletedTask;
    }
}
```

### 4.2 Multiple Handlers for Same Event

```csharp
// Handler 1: Send notification
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

// Handler 2: Publish to NATS
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

// Handler 3: Update statistics
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

## 5. Event Publishing Mechanism

### 5.1 Publishing in DbContext

```csharp
public abstract class WedaDbContext : DbContext
{
    private readonly IMediator _mediator;

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // Extract Domain Events before saving
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        // Execute save
        var result = await base.SaveChangesAsync(cancellationToken);

        // Publish events after successful save
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
```

### 5.2 Publishing Timing

| Timing | Pros | Cons |
|--------|------|------|
| After save | Ensures data consistency | Event handler failure doesn't affect main operation |
| Before save | Can reject operation in event handler | May cause inconsistency |

---

## 6. NATS Integration

### 6.1 NATS Event Definition

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

### 6.2 Publishing to NATS

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

## Quick Reference

### Event Handling Flow

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

### File Organization

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

### Design Principles

| Principle | Description |
|-----------|-------------|
| Immutable | Events should be records, immutable after creation |
| Past tense naming | Use past tense to indicate something that happened |
| Contain necessary info | Event should contain all information needed for processing |
| Handler idempotency | Handlers should be safe to execute multiple times |

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [01-domain-layer.md](01-domain-layer.md) - Domain Layer (Entity & Aggregate)
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer (DbContext)
- [Mediator Documentation](https://github.com/martinothamar/Mediator)
