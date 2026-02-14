---
title: WEDA Clean Architecture Template Learning Guide
description: A comprehensive guide from zero to expert for internal team training
keywords: [Clean Architecture, DDD, CQRS, Mediator, .NET]
sidebar_position: 1
---

# WEDA Clean Architecture Template - Learning Guide

> From Zero to Expert: A comprehensive guide for internal team training

## Part 1: Foundation

### 1. Clean Architecture Overview
- Uncle Bob's Clean Architecture principles
- Dependency Rule: dependencies point inward
- Independence from frameworks, UI, databases, and external agencies
- Testability by design

### 2. Project Structure & Layer Dependencies
- Solution organization: Domain → Application → Infrastructure → Api
- Project references and dependency direction
- Contracts project: shared DTOs between layers
- Why separation matters for maintainability

### 3. Domain-Driven Design (DDD) Basics
- Ubiquitous Language
- Bounded Context
- Entities vs Value Objects
- Aggregates and Aggregate Roots

---

## Part 2: Domain Layer

### 4. Entity & Aggregate Root
- Entity base class with Id and domain events
- Aggregate Root as consistency boundary
- Encapsulation: private setters, controlled state changes
- Example: `Employee` as an Aggregate Root

### 5. Value Objects
- Immutability and equality by value
- Self-validating objects
- When to use Value Objects vs Entities
- Example: `Email`, `Money`, `Address`

### 6. Factory Method Pattern
- Why constructors should be private
- `Create()` method returning `ErrorOr<T>`
- Validation at creation time
- Domain invariants enforcement

### 7. Domain Errors (ErrorOr Pattern)
- Railway-oriented programming
- `ErrorOr<T>` for explicit error handling
- Static error definitions in `*Errors` classes
- Error types: Validation, NotFound, Conflict, Unauthorized
- See [09-error-handling-guide.md](09-error-handling-guide.md) for details

### 8. Domain Events
- What are Domain Events
- `IDomainEvent` interface
- Raising events within Aggregate Root
- Event collection and publishing pattern
- See [11-domain-events-guide.md](11-domain-events-guide.md) for details

---

## Part 3: Application Layer

### 9. CQRS Pattern (Command Query Responsibility Segregation)
- Commands: write operations that change state
- Queries: read operations that return data
- Why separate Commands from Queries
- Folder structure: `Commands/` and `Queries/`

### 10. Mediator & Request/Handler Pattern
- `IRequest<T>` and `IRequestHandler<TRequest, TResponse>`
- Decoupling sender from handler
- One handler per request
- Registration via assembly scanning

### 11. Repository Interface (IRepository<T>)
- Generic repository pattern
- `IRepository<T>` base interface
- Specialized interfaces: `IEmployeeRepository`
- Why interfaces belong in Application layer

### 12. Pipeline Behaviors (Validation, Authorization)
- Mediator pipeline concept
- `IPipelineBehavior<TRequest, TResponse>`
- Cross-cutting concerns: logging, validation, authorization
- Execution order and chaining

### 13. FluentValidation
- `AbstractValidator<T>` for request validation
- Validation rules and error messages
- Integration with Mediator pipeline
- Custom validators
- See [10-validation-guide.md](10-validation-guide.md) for details

---

## Part 4: Infrastructure Layer

### 14. Generic Repository Implementation
- `GenericRepository<T>` implementing `IRepository<T>`
- Entity Framework Core integration
- CRUD operations implementation
- Unit of Work pattern (optional)

### 15. Entity Framework Core & DbContext
- `AppDbContext` configuration
- Domain event publishing on SaveChanges
- Connection string management
- Database providers: SQLite, PostgreSQL, MongoDB

### 16. Database Configuration (EF Configurations)
- `IEntityTypeConfiguration<T>`
- Fluent API for mapping
- Value converters for Value Objects
- Indexes and constraints

### 17. Dependency Injection Module Pattern
- `*Module.cs` naming convention
- Extension methods for `IServiceCollection`
- Service lifetime: Singleton, Scoped, Transient
- Configuration binding with `IOptions<T>`

---

## Part 5: API Layer

### 18. Controllers & Routing
- `[ApiController]` attribute
- Route conventions: `[Route("api/[controller]")]`
- Action methods and HTTP verbs
- Injecting Mediator `ISender`

### 19. Contracts (DTOs) & Mapping
- Request and Response DTOs
- Why Contracts are separate from Domain
- Manual mapping vs Mapperly
- API versioning considerations

### 20. Error Handling & ProblemDetails
- Global exception handling
- `ProblemDetails` RFC 7807
- Mapping `ErrorOr` to HTTP responses
- Consistent error format

### 21. Authentication & Authorization
- JWT Bearer authentication
- `[Authorize]` attribute
- Policy-based authorization
- Current user provider pattern
- See [06-authentication-guide.md](06-authentication-guide.md) for details

---

## Part 6: Testing

### 22. Unit Testing (Domain, Application)
- Testing Domain logic in isolation
- Mocking dependencies with NSubstitute
- Testing Command/Query handlers
- FluentAssertions for readable assertions

### 23. Integration Testing (WebApplicationFactory)
- `WebApplicationFactory<T>` setup
- In-memory database for testing
- HTTP client testing
- Test fixtures and collections

### 24. Test Utilities & Factories
- `TestCommon` shared project
- Object mothers and builders
- `CurrentUserFactory` for auth testing
- Reusable test helpers
- See [07-testing-guide.md](07-testing-guide.md) for details

---

## Part 7: Advanced Topics

### 25. ServiceFramework Integration (NATS, JetStream)
- EdgeSync.ServiceFramework packages
- NatsService for Request/Response
- BaseEventHandler for event handling
- JetStreamClient for event publishing

### 26. Multiple Database Support
- Database abstraction strategy
- InMemory repository for development
- PostgreSQL for production
- MongoDB as alternative
- See [05-database-switch-guide.md](05-database-switch-guide.md) for details

### 27. DevContainer & Docker Compose
- `.devcontainer/` configuration
- `docker-compose.yml` for dependencies
- Development environment setup
- Consistent team environments

### 28. dotnet new Template Configuration
- `template.json` structure
- Template parameters and symbols
- Conditional file inclusion
- Publishing to NuGet
- See [08-template-usage-guide.md](08-template-usage-guide.md) for details

---

## Quick Reference

### Layer Dependencies
```
Api → Application → Domain
 ↓         ↓
Infrastructure
     ↓
   Domain
```

### Key Patterns Used
| Pattern | Location | Purpose |
|---------|----------|---------|
| Factory Method | Domain | Controlled entity creation |
| Repository | Application/Infrastructure | Data access abstraction |
| CQRS | Application | Separate read/write operations |
| Mediator | Application | Decouple request handling |
| Module | All layers | Organize DI registration |

### Error Handling Flow
```
Domain Error → ErrorOr<T> → Handler → Controller → ProblemDetails → HTTP Response
```

---

## Recommended Learning Path

1. **Week 1**: Part 1-2 (Foundation + Domain)
2. **Week 2**: Part 3 (Application Layer)
3. **Week 3**: Part 4-5 (Infrastructure + API)
4. **Week 4**: Part 6 (Testing)
5. **Week 5**: Part 7 (Advanced Topics)

---

## Resources

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [ErrorOr Library](https://github.com/amantinband/error-or)
- [Mediator Documentation](https://github.com/martinothamar/Mediator)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [Amantinband Clean Architecture Template](https://github.com/amantinband/clean-architecture)

<\!-- test -->
