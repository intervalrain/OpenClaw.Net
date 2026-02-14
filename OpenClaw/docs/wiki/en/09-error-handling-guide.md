---
title: Error Handling Guide
description: Implementation guide for ErrorOr Pattern and ProblemDetails error handling
keywords: [ErrorOr, Error Handling, ProblemDetails, Domain Errors]
sidebar_position: 10
---

# Error Handling Guide

> Learn how to implement consistent error handling using ErrorOr Pattern

## Overview

Weda Template uses the [ErrorOr](https://github.com/amantinband/error-or) package to implement Railway-Oriented Programming, providing:
- Explicit error type definitions
- Avoiding exception-based control flow
- Consistent HTTP response format (ProblemDetails)

```
src/
├── OpenClaw.Domain/
│   └── {Aggregate}/Errors/
│       └── {Aggregate}Errors.cs   # Domain error definitions
├── Weda.Core/Api/
│   └── ApiController.cs           # Error to HTTP Response mapping
└── OpenClaw.Application/
    └── {Feature}/Commands/
        └── {Command}Handler.cs    # Returns ErrorOr<T>
```

---

## 1. ErrorOr Basics

### 1.1 ErrorOr<T> Type

`ErrorOr<T>` represents either "success returning T" or "failure returning a list of Errors":

```csharp
// Success
ErrorOr<Employee> success = employee;

// Failure
ErrorOr<Employee> failure = EmployeeErrors.NotFound;

// Multiple errors
ErrorOr<Employee> failures = new List<Error>
{
    EmployeeErrors.EmptyName,
    EmployeeErrors.InvalidEmailFormat
};
```

### 1.2 Error Types

```csharp
public enum ErrorType
{
    Failure,      // General failure
    Unexpected,   // Unexpected error
    Validation,   // Validation error → 400 Bad Request
    Conflict,     // Conflict error → 409 Conflict
    NotFound,     // Not found → 404 Not Found
    Unauthorized, // Unauthorized → 403 Forbidden
    Forbidden     // Forbidden → 403 Forbidden
}
```

### 1.3 Creating Errors

```csharp
// Using factory methods
var notFound = Error.NotFound(
    code: "Employee.NotFound",
    description: "The employee was not found.");

var validation = Error.Validation(
    code: "Employee.EmptyName",
    description: "Employee name cannot be empty.");

var conflict = Error.Conflict(
    code: "Employee.DuplicateEmail",
    description: "An employee with this email already exists.");

var unauthorized = Error.Unauthorized(
    code: "Auth.InvalidCredentials",
    description: "Invalid email or password.");
```

---

## 2. Domain Errors

### 2.1 Defining Error Classes

Define aggregate-specific errors in the Domain Layer:

```csharp
namespace OpenClaw.Domain.Employees.Errors;

public static class EmployeeErrors
{
    // Not Found Errors
    public static readonly Error NotFound = Error.NotFound(
        code: "Employee.NotFound",
        description: "The employee with the specified ID was not found.");

    public static readonly Error SupervisorNotFound = Error.NotFound(
        code: "Employee.SupervisorNotFound",
        description: "The specified supervisor was not found.");

    // Validation Errors - Name
    public static readonly Error EmptyName = Error.Validation(
        code: "Employee.EmptyName",
        description: "Employee name cannot be empty.");

    public static readonly Error NameTooLong = Error.Validation(
        code: "Employee.NameTooLong",
        description: "Employee name cannot exceed 100 characters.");

    // Validation Errors - Email
    public static readonly Error InvalidEmailFormat = Error.Validation(
        code: "Employee.InvalidEmailFormat",
        description: "The email format is invalid.");

    public static readonly Error DuplicateEmail = Error.Conflict(
        code: "Employee.DuplicateEmail",
        description: "An employee with this email already exists.");

    // Business Rule Errors
    public static readonly Error CannotBeSelfSupervisor = Error.Validation(
        code: "Employee.CannotBeSelfSupervisor",
        description: "An employee cannot be their own supervisor.");

    public static readonly Error CircularSupervisorReference = Error.Validation(
        code: "Employee.CircularSupervisorReference",
        description: "Assigning this supervisor would create a circular reference.");

    public static readonly Error HasSubordinates = Error.Conflict(
        code: "Employee.HasSubordinates",
        description: "Cannot delete an employee who has subordinates.");
}
```

### 2.2 Error Naming Convention

```
{Aggregate}.{ErrorName}
```

Examples:
- `Employee.NotFound`
- `Employee.DuplicateEmail`
- `User.InvalidCredentials`
- `Order.AlreadyShipped`

---

## 3. Using in Handlers

### 3.1 Basic Return

```csharp
public class GetEmployeeQueryHandler(IEmployeeRepository repository)
    : IRequestHandler<GetEmployeeQuery, ErrorOr<EmployeeResponse>>
{
    public async ValueTask<ErrorOr<EmployeeResponse>> Handle(
        GetEmployeeQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(
            request.Id, cancellationToken);

        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        return employee.ToResponse();
    }
}
```

### 3.2 Chaining Multiple Operations

```csharp
public async ValueTask<ErrorOr<EmployeeResponse>> Handle(
    CreateEmployeeCommand request,
    CancellationToken cancellationToken)
{
    // 1. Check if email exists
    if (await repository.ExistsWithEmailAsync(
        Email.Create(request.Email).Value, cancellationToken))
    {
        return EmployeeErrors.DuplicateEmail;
    }

    // 2. Create Employee (may fail)
    var employeeResult = Employee.Create(
        request.Name,
        request.Email,
        request.Department,
        request.Position,
        request.HireDate);

    if (employeeResult.IsError)
    {
        return employeeResult.Errors;
    }

    // 3. Save
    await repository.AddAsync(employeeResult.Value, cancellationToken);

    return employeeResult.Value.ToResponse();
}
```

### 3.3 Using Match

```csharp
var result = await handler.Handle(command, cancellationToken);

return result.Match(
    success => Ok(success),
    errors => Problem(errors));
```

### 3.4 Using Then for Chaining

```csharp
return await Employee.Create(request.Name, request.Email, ...)
    .ThenAsync(async employee =>
    {
        await repository.AddAsync(employee, cancellationToken);
        return employee;
    })
    .Then(employee => employee.ToResponse());
```

---

## 4. API Layer Error Handling

### 4.1 ApiController Base Class

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class ApiController : ControllerBase
{
    protected ActionResult Problem(List<Error> errors)
    {
        if (errors.Count is 0)
        {
            return Problem();
        }

        if (errors.All(error => error.Type == ErrorType.Validation))
        {
            return ValidationProblem(errors);
        }

        return Problem(errors[0]);
    }

    private ObjectResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Problem(statusCode: statusCode, title: error.Description);
    }

    private ActionResult ValidationProblem(List<Error> errors)
    {
        var modelStateDictionary = new ModelStateDictionary();

        errors.ForEach(error =>
            modelStateDictionary.AddModelError(error.Code, error.Description));

        return ValidationProblem(modelStateDictionary);
    }
}
```

### 4.2 Controller Usage

```csharp
public class EmployeesController(ISender mediator) : ApiController
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetEmployee(int id)
    {
        var result = await mediator.Send(new GetEmployeeQuery(id));

        return result.Match(Ok, Problem);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeRequest request)
    {
        var command = new CreateEmployeeCommand(
            request.Name,
            request.Email,
            request.Department,
            request.Position,
            request.HireDate);

        var result = await mediator.Send(command);

        return result.Match(
            employee => CreatedAtAction(
                nameof(GetEmployee),
                new { id = employee.Id },
                employee),
            Problem);
    }
}
```

---

## 5. ProblemDetails Response

### 5.1 Error Type Mapping

| ErrorType | HTTP Status | Description |
|-----------|-------------|-------------|
| `Validation` | 400 Bad Request | Input validation failed |
| `NotFound` | 404 Not Found | Resource doesn't exist |
| `Conflict` | 409 Conflict | Resource conflict (duplicate) |
| `Unauthorized` | 403 Forbidden | No permission to access |
| `Failure` | 500 Internal Server Error | General error |

### 5.2 Response Examples

**Single Error (404)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "The employee with the specified ID was not found.",
  "status": 404
}
```

**Validation Errors (400)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Employee.EmptyName": ["Employee name cannot be empty."],
    "Employee.InvalidEmailFormat": ["The email format is invalid."]
  }
}
```

---

## 6. Factory Method in Entities

### 6.1 Creating Entities with ErrorOr

```csharp
public class Employee : AggregateRoot<int>
{
    private Employee() { }

    public static ErrorOr<Employee> Create(
        string name,
        string email,
        string department,
        string position,
        DateTime hireDate,
        int? supervisorId = null)
    {
        // Create Value Objects (may fail)
        var nameResult = EmployeeName.Create(name);
        var emailResult = Email.Create(email);
        var departmentResult = Department.Create(department);

        // Collect all errors
        var errors = new List<Error>();

        if (nameResult.IsError)
            errors.AddRange(nameResult.Errors);
        if (emailResult.IsError)
            errors.AddRange(emailResult.Errors);
        if (departmentResult.IsError)
            errors.AddRange(departmentResult.Errors);

        if (errors.Count > 0)
            return errors;

        // Create Entity
        var employee = new Employee
        {
            Name = nameResult.Value,
            Email = emailResult.Value,
            Department = departmentResult.Value,
            Position = position,
            HireDate = hireDate,
            SupervisorId = supervisorId,
            Status = EmployeeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        employee.RaiseDomainEvent(new EmployeeCreatedEvent(employee));

        return employee;
    }
}
```

---

## Quick Reference

### Error Handling Flow

```
Domain Error → ErrorOr<T> → Handler → Controller → ProblemDetails → HTTP Response
```

### Common Patterns

```csharp
// Early return
if (entity is null)
    return EntityErrors.NotFound;

// Match Pattern
return result.Match(
    success => Ok(success),
    errors => Problem(errors));

// Then chaining
return await CreateEntity()
    .ThenAsync(async e => await Save(e))
    .Then(e => e.ToResponse());
```

### Error Categories

| Category | Description | Examples |
|----------|-------------|----------|
| Validation | Input format errors | `EmptyName`, `InvalidEmailFormat` |
| NotFound | Resource doesn't exist | `NotFound`, `SupervisorNotFound` |
| Conflict | Business rule conflicts | `DuplicateEmail`, `AlreadyActive` |
| Unauthorized | Insufficient permissions | `InvalidCredentials` |

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [01-domain-layer.md](01-domain-layer.md) - Domain Layer (Entity & Error Definition)
- [02-application-layer.md](02-application-layer.md) - Application Layer (Handler Implementation)
- [ErrorOr GitHub](https://github.com/amantinband/error-or)
- [RFC 7807 - Problem Details](https://datatracker.ietf.org/doc/html/rfc7807)
