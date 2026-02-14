---
title: Weda.Core Overview
description: Core library providing DDD abstractions, CQRS pipeline, NATS messaging, and API infrastructure
keywords: [Weda.Core, DDD, CQRS, NATS, Clean Architecture, Infrastructure]
sidebar_position: 1
---

# Weda.Core Overview

> Shared infrastructure library for building Clean Architecture applications with DDD, CQRS, and event-driven messaging

## What is Weda.Core?

Weda.Core is a foundational library that provides all the base classes, abstractions, and infrastructure needed to build applications following Clean Architecture and Domain-Driven Design principles. It eliminates boilerplate code and enforces consistent patterns across projects.

```
Weda.Core/
├── Domain/              # DDD base classes (Entity, AggregateRoot, etc.)
├── Application/         # CQRS behaviors, security, interfaces
├── Infrastructure/      # Persistence, NATS messaging, middleware
├── Api/                 # REST controller base, Swagger configuration
└── WedaCoreModule.cs    # Service registration and middleware setup
```

---

## Architecture Overview

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

## 1. Domain Abstractions

Weda.Core provides the foundation for Domain-Driven Design:

### Entity<TId>

Base class for all domain entities with identity-based equality.

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

Entity that serves as the aggregate boundary with domain event support.

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

Generic repository interface for data access abstraction.

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

Marker interface for domain events, extending Mediator's `INotification`.

```csharp
public interface IDomainEvent : INotification { }
```

---

## 2. Application Layer Components

### Pipeline Behaviors

Weda.Core provides Mediator pipeline behaviors for cross-cutting concerns:

**ValidationBehavior**
- Intercepts all requests before handler execution
- Uses FluentValidation to validate request objects
- Returns `ErrorOr` with validation errors if validation fails

**AuthorizationBehavior**
- Intercepts `IAuthorizeableRequest<T>` implementations
- Reads `[Authorize]` attributes for required roles, permissions, policies
- Delegates to `IAuthorizationService` for authorization checks

### Security Infrastructure

```csharp
// Attribute for declarative authorization
[Authorize(Roles = "Admin", Permissions = "employees:write")]
public record CreateEmployeeCommand(...) : IAuthorizeableRequest<ErrorOr<EmployeeDto>>;

// Interface for authorization service
public interface IAuthorizationService
{
    ErrorOr<Success> AuthorizeCurrentUser<T>(
        IAuthorizeableRequest<T> request,
        List<string> requiredRoles,
        List<string> requiredPermissions,
        List<string> requiredPolicies);
}

// Interface for JWT token generation
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

### Application Interfaces

```csharp
// Abstraction for testable time operations
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
```

---

## 3. Infrastructure Components

### WedaDbContext

Base DbContext with automatic domain event publishing and eventual consistency support.

```csharp
public abstract class WedaDbContext : DbContext
{
    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Collect domain events from aggregate roots
        var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        // 2. Save changes to database
        var result = await base.SaveChangesAsync(cancellationToken);

        // 3. Queue or publish domain events
        if (IsUserOnline)
            QueueEventsForEventualConsistency(domainEvents);
        else
            await PublishEventsImmediately(domainEvents);

        return result;
    }
}
```

### GenericRepository<T, TId, TDbContext>

Base repository implementation with EF Core.

```csharp
public class GenericRepository<T, TId, TDbContext> : IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
    where TDbContext : DbContext
{
    protected readonly TDbContext DbContext;
    protected readonly DbSet<T> DbSet;

    // Standard CRUD operations with automatic SaveChanges
}
```

### Eventual Consistency Middleware

Ensures domain events are published within the same transaction.

```
HTTP Request
     ↓
Begin Transaction
     ↓
Controller → Handler → Repository.SaveChanges()
     ↓
Domain events queued in HttpContext
     ↓
Response sent to client
     ↓
Publish queued domain events
     ↓
Commit Transaction
```

### NATS Messaging

Complete event-driven messaging infrastructure with multiple patterns:

| Pattern | Description |
|---------|-------------|
| Request-Reply | Synchronous RPC-style communication |
| Core Pub-Sub | Fire-and-forget messaging |
| JetStream Consume | Persistent, guaranteed delivery with continuous consumption |
| JetStream Fetch | Batch message processing |

---

## 4. API Components

### ApiController

Base controller with automatic error mapping to ProblemDetails.

```csharp
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public class ApiController : ControllerBase
{
    protected ActionResult Problem(List<Error> errors)
    {
        // Maps ErrorOr errors to appropriate HTTP status codes
        // - Validation → 400 Bad Request
        // - NotFound → 404 Not Found
        // - Conflict → 409 Conflict
        // - Unauthorized → 401 Unauthorized
        // - Forbidden → 403 Forbidden
    }
}
```

### EventController

Base class for NATS event-driven endpoints.

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

### Swagger Integration

- Auto-generates OpenAPI documentation
- Injects request/response examples via `IExamplesProvider<T>`
- Adds Bearer token security for authorized endpoints

---

## 5. Module Registration

### Adding Weda.Core to Your Application

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

### AddWedaCore Parameters

| Parameter | Description |
|-----------|-------------|
| `TApiMarker` | API assembly marker for EventController scanning |
| `TContractsMarker` | Contracts assembly marker for Swagger examples |
| `TApplicationMarker` | Application assembly marker for validators |
| `configuration` | IConfiguration for reading settings |
| `mediatorAction` | Callback to configure Mediator options |
| `optionsAction` | Callback to configure WedaCoreOptions |

### WedaCoreOptions (AddWedaCore)

| Option | Description |
|--------|-------------|
| `XmlCommentAssemblies` | Assemblies containing XML comments for Swagger |
| `OpenApiInfo` | OpenAPI document info (title, version) |

### WedaCoreMiddlewareOptions (UseWedaCore)

| Option | Description |
|--------|-------------|
| `EnsureDatabaseCreated` | Auto-create database on startup |
| `SwaggerEndpointUrl` | Swagger JSON endpoint URL |
| `SwaggerEndpointName` | Swagger endpoint display name |
| `RoutePrefix` | Swagger UI route prefix |

---

## 6. Key Dependencies

Weda.Core integrates with these libraries (all MIT licensed):

| Library | Purpose |
|---------|---------|
| **ErrorOr** | Functional error handling |
| **Mediator** | CQRS and pipeline behaviors (source generator based, high performance) |
| **Mapperly** | Object mapping via source generators (zero reflection) |
| **FluentValidation** | Request validation |
| **NATS.Net** | Messaging and event streaming |
| **Entity Framework Core** | Database persistence |
| **Asp.Versioning** | API versioning |
| **Swashbuckle** | OpenAPI/Swagger documentation |

---

## 7. Design Patterns Implemented

| Pattern | Implementation |
|---------|----------------|
| Domain-Driven Design | Entity, AggregateRoot, Domain Events, Repository |
| CQRS | Command/Query separation via Mediator |
| Repository | Generic and specialized repository abstractions |
| Eventual Consistency | Middleware-based domain event publishing |
| Pipeline | Validation and authorization behaviors |
| Factory Method | Entity creation with ErrorOr |
| Event-Driven | Domain events and NATS messaging |

---

## Related Resources

- [01-domain-layer.md](01-domain-layer.md) - Domain Layer Implementation Guide
- [02-application-layer.md](02-application-layer.md) - Application Layer Guide
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer Guide
- [04-api-layer.md](04-api-layer.md) - API Layer Guide
- [GUIDE.md](GUIDE.md) - Learning Guide Overview
