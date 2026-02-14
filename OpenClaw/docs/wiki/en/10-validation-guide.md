---
title: Validation Guide
description: Implementation guide for FluentValidation and Pipeline Behavior validation
keywords: [FluentValidation, Validation, Pipeline Behavior, Request Validation]
sidebar_position: 11
---

# Validation Guide

> Learn how to implement request validation using FluentValidation

## Overview

Weda Template uses [FluentValidation](https://docs.fluentvalidation.net/) with Mediator Pipeline Behavior to implement input validation, providing:
- Declarative validation rule definitions
- Automatic validation execution (via Pipeline Behavior)
- Error returns integrated with ErrorOr

```
src/
├── Weda.Core/Application/Behaviors/
│   └── ValidationBehavior.cs       # Validation Pipeline Behavior
└── OpenClaw.Application/
    └── {Feature}/Commands/
        ├── {Command}.cs            # Command definition
        └── {Command}Validator.cs   # Validator
```

---

## 1. FluentValidation Basics

### 1.1 Install Package

```bash
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

### 1.2 Basic Validator

```csharp
using FluentValidation;

public class CreateEmployeeCommandValidator
    : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email format.");

        RuleFor(x => x.Department)
            .NotEmpty()
            .WithMessage("Department is required.");

        RuleFor(x => x.Position)
            .NotEmpty()
            .WithMessage("Position is required.");

        RuleFor(x => x.HireDate)
            .NotEmpty()
            .WithMessage("Hire date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage("Hire date cannot be in the future.");
    }
}
```

---

## 2. ValidationBehavior

### 2.1 Pipeline Behavior Implementation

```csharp
public class ValidationBehavior<TRequest, TResponse>(
    IValidator<TRequest>? validator = null)
    : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IErrorOr
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // If no validator, proceed to next step
        if (validator is null)
        {
            return await next(request, cancellationToken);
        }

        // Execute validation
        var validationResult = await validator.ValidateAsync(
            request, cancellationToken);

        if (validationResult.IsValid)
        {
            return await next(request, cancellationToken);
        }

        // Convert to ErrorOr Errors
        var errors = validationResult.Errors
            .ConvertAll(error => Error.Validation(
                code: error.PropertyName,
                description: error.ErrorMessage));

        return (dynamic)errors;
    }
}
```

### 2.2 Register Behavior

```csharp
// In Application Module
services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

// Register Pipeline Behaviors (order matters)
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
```

### 2.3 Register Validators

```csharp
// Auto-scan and register all validators
services.AddValidatorsFromAssembly(typeof(IApplicationMarker).Assembly);
```

---

## 3. Validation Rules

### 3.1 Common Rules

```csharp
public class UserCommandValidator : AbstractValidator<UserCommand>
{
    public UserCommandValidator()
    {
        // Required
        RuleFor(x => x.Name)
            .NotEmpty();

        // Length constraint
        RuleFor(x => x.Name)
            .Length(2, 100);

        // Email format
        RuleFor(x => x.Email)
            .EmailAddress();

        // Regular expression
        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$")
            .WithMessage("Phone must be 10 digits.");

        // Numeric range
        RuleFor(x => x.Age)
            .InclusiveBetween(18, 120);

        // Enum validation
        RuleFor(x => x.Status)
            .IsInEnum();

        // Conditional validation
        RuleFor(x => x.ManagerId)
            .NotEmpty()
            .When(x => x.Role == "Employee");
    }
}
```

### 3.2 Custom Error Messages

```csharp
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage("Email is required.")
    .EmailAddress()
    .WithMessage("Please provide a valid email address.")
    .WithErrorCode("User.InvalidEmail");
```

### 3.3 Custom Validation Rules

```csharp
public class CreateEmployeeCommandValidator
    : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Email)
            .Must(BeValidCompanyEmail)
            .WithMessage("Email must be a company email address.");
    }

    private bool BeValidCompanyEmail(string email)
    {
        return email.EndsWith("@company.com");
    }
}
```

### 3.4 Complex Object Validation

```csharp
public class OrderCommandValidator : AbstractValidator<OrderCommand>
{
    public OrderCommandValidator()
    {
        // Nested object
        RuleFor(x => x.ShippingAddress)
            .NotNull()
            .SetValidator(new AddressValidator());

        // Collection validation
        RuleForEach(x => x.OrderItems)
            .SetValidator(new OrderItemValidator());

        // Collection count
        RuleFor(x => x.OrderItems)
            .NotEmpty()
            .WithMessage("Order must have at least one item.");
    }
}

public class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator()
    {
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.PostalCode).Matches(@"^\d{5}$");
    }
}
```

---

## 4. Async Validation

### 4.1 Async Rules

```csharp
public class CreateEmployeeCommandValidator
    : AbstractValidator<CreateEmployeeCommand>
{
    private readonly IEmployeeRepository _repository;

    public CreateEmployeeCommandValidator(IEmployeeRepository repository)
    {
        _repository = repository;

        RuleFor(x => x.Email)
            .MustAsync(BeUniqueEmail)
            .WithMessage("An employee with this email already exists.");
    }

    private async Task<bool> BeUniqueEmail(
        string email,
        CancellationToken cancellationToken)
    {
        return !await _repository.ExistsWithEmailAsync(
            Email.Create(email).Value,
            cancellationToken);
    }
}
```

### 4.2 Important Note

> **Important**: Async validation executes database queries. Consider handling uniqueness checks and similar business logic in the Handler rather than the Validator.

---

## 5. Validation Response

### 5.1 Error Format

When validation fails, ValidationBehavior returns `List<Error>`:

```csharp
var errors = validationResult.Errors
    .ConvertAll(error => Error.Validation(
        code: error.PropertyName,
        description: error.ErrorMessage));
```

### 5.2 HTTP Response

Through ApiController's `Problem` method, validation errors are converted to 400 Bad Request:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["Name is required."],
    "Email": ["Invalid email format."],
    "HireDate": ["Hire date cannot be in the future."]
  }
}
```

---

## 6. Best Practices

### 6.1 Validator File Organization

```
Application/
└── Employees/
    └── Commands/
        ├── CreateEmployee/
        │   ├── CreateEmployeeCommand.cs
        │   ├── CreateEmployeeCommandHandler.cs
        │   └── CreateEmployeeCommandValidator.cs
        └── UpdateEmployee/
            ├── UpdateEmployeeCommand.cs
            ├── UpdateEmployeeCommandHandler.cs
            └── UpdateEmployeeCommandValidator.cs
```

### 6.2 Shared Validation Rules

```csharp
public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email format.")
            .MaximumLength(256)
            .WithMessage("Email cannot exceed 256 characters.");
    }

    public static IRuleBuilderOptions<T, string> ValidName<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters.");
    }
}

// Usage
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Email).ValidEmail();
    }
}
```

### 6.3 Validation Levels

| Level | Location | What to Validate |
|-------|----------|------------------|
| Request | Validator | Format, required, length, range |
| Domain | Value Object | Business rules, invariants |
| Application | Handler | Cross-entity rules, uniqueness |

---

## Quick Reference

### Common Validation Methods

| Method | Description |
|--------|-------------|
| `NotEmpty()` | Non-empty (strings, collections) |
| `NotNull()` | Non-null |
| `Length(min, max)` | Length range |
| `MaximumLength(max)` | Maximum length |
| `EmailAddress()` | Email format |
| `Matches(regex)` | Regular expression |
| `InclusiveBetween(min, max)` | Numeric range (inclusive) |
| `GreaterThan(value)` | Greater than |
| `LessThanOrEqualTo(value)` | Less than or equal to |
| `IsInEnum()` | Valid enum value |
| `Must(predicate)` | Custom condition |
| `MustAsync(predicate)` | Async custom condition |
| `When(condition)` | Conditional validation |
| `SetValidator(validator)` | Nested validator |

### Pipeline Execution Order

```
Request → ValidationBehavior → AuthorizationBehavior → Handler
                ↓ (failure)
            Return Errors
```

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [02-application-layer.md](02-application-layer.md) - Application Layer (Pipeline Behavior)
- [09-error-handling-guide.md](09-error-handling-guide.md) - Error Handling Guide
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
