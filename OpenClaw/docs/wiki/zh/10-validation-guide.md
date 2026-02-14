---
title: 驗證機制指南
description: FluentValidation 與 Pipeline Behavior 驗證機制的實作指南
keywords: [FluentValidation, Validation, Pipeline Behavior, Request Validation]
sidebar_position: 11
---

# 驗證機制指南

> 學習如何使用 FluentValidation 實作 Request 驗證

## 概觀

Weda Template 使用 [FluentValidation](https://docs.fluentvalidation.net/) 搭配 Mediator Pipeline Behavior 實作輸入驗證，提供：
- 宣告式驗證規則定義
- 自動執行驗證（透過 Pipeline Behavior）
- 與 ErrorOr 整合的錯誤回傳

```
src/
├── Weda.Core/Application/Behaviors/
│   └── ValidationBehavior.cs       # 驗證 Pipeline Behavior
└── OpenClaw.Application/
    └── {Feature}/Commands/
        ├── {Command}.cs            # Command 定義
        └── {Command}Validator.cs   # 驗證器
```

---

## 1. FluentValidation 基礎

### 1.1 安裝套件

```bash
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

### 1.2 基本 Validator

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

### 2.1 Pipeline Behavior 實作

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
        // 如果沒有 Validator，直接執行下一步
        if (validator is null)
        {
            return await next(request, cancellationToken);
        }

        // 執行驗證
        var validationResult = await validator.ValidateAsync(
            request, cancellationToken);

        if (validationResult.IsValid)
        {
            return await next(request, cancellationToken);
        }

        // 轉換為 ErrorOr Errors
        var errors = validationResult.Errors
            .ConvertAll(error => Error.Validation(
                code: error.PropertyName,
                description: error.ErrorMessage));

        return (dynamic)errors;
    }
}
```

### 2.2 註冊 Behavior

```csharp
// 在 Application Module 中
services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

// 註冊 Pipeline Behaviors（順序重要）
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
```

### 2.3 註冊 Validators

```csharp
// 自動掃描並註冊所有 Validator
services.AddValidatorsFromAssembly(typeof(IApplicationMarker).Assembly);
```

---

## 3. 驗證規則

### 3.1 常用規則

```csharp
public class UserCommandValidator : AbstractValidator<UserCommand>
{
    public UserCommandValidator()
    {
        // 必填
        RuleFor(x => x.Name)
            .NotEmpty();

        // 長度限制
        RuleFor(x => x.Name)
            .Length(2, 100);

        // Email 格式
        RuleFor(x => x.Email)
            .EmailAddress();

        // 正則表達式
        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$")
            .WithMessage("Phone must be 10 digits.");

        // 數值範圍
        RuleFor(x => x.Age)
            .InclusiveBetween(18, 120);

        // 列舉驗證
        RuleFor(x => x.Status)
            .IsInEnum();

        // 條件式驗證
        RuleFor(x => x.ManagerId)
            .NotEmpty()
            .When(x => x.Role == "Employee");
    }
}
```

### 3.2 自訂錯誤訊息

```csharp
RuleFor(x => x.Email)
    .NotEmpty()
    .WithMessage("Email is required.")
    .EmailAddress()
    .WithMessage("Please provide a valid email address.")
    .WithErrorCode("User.InvalidEmail");
```

### 3.3 自訂驗證規則

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

### 3.4 複雜物件驗證

```csharp
public class OrderCommandValidator : AbstractValidator<OrderCommand>
{
    public OrderCommandValidator()
    {
        // 巢狀物件
        RuleFor(x => x.ShippingAddress)
            .NotNull()
            .SetValidator(new AddressValidator());

        // 集合驗證
        RuleForEach(x => x.OrderItems)
            .SetValidator(new OrderItemValidator());

        // 集合數量
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

## 4. 非同步驗證

### 4.1 非同步規則

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

### 4.2 注意事項

> **重要**：非同步驗證會執行資料庫查詢，建議在 Handler 中處理重複檢查等業務邏輯，而非在 Validator 中。

---

## 5. 驗證回應

### 5.1 錯誤格式

驗證失敗時，ValidationBehavior 會回傳 `List<Error>`：

```csharp
var errors = validationResult.Errors
    .ConvertAll(error => Error.Validation(
        code: error.PropertyName,
        description: error.ErrorMessage));
```

### 5.2 HTTP 回應

透過 ApiController 的 `Problem` 方法，驗證錯誤會轉換為 400 Bad Request：

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

## 6. 最佳實踐

### 6.1 Validator 檔案組織

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

### 6.2 共用驗證規則

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

// 使用
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.Email).ValidEmail();
    }
}
```

### 6.3 驗證層級

| 層級 | 位置 | 驗證內容 |
|------|------|----------|
| Request | Validator | 格式、必填、長度、範圍 |
| Domain | Value Object | 業務規則、Invariants |
| Application | Handler | 跨 Entity 規則、唯一性 |

---

## 快速參考

### 常用驗證方法

| 方法 | 說明 |
|------|------|
| `NotEmpty()` | 非空（字串、集合） |
| `NotNull()` | 非 null |
| `Length(min, max)` | 長度範圍 |
| `MaximumLength(max)` | 最大長度 |
| `EmailAddress()` | Email 格式 |
| `Matches(regex)` | 正則表達式 |
| `InclusiveBetween(min, max)` | 數值範圍（含邊界） |
| `GreaterThan(value)` | 大於 |
| `LessThanOrEqualTo(value)` | 小於等於 |
| `IsInEnum()` | 有效列舉值 |
| `Must(predicate)` | 自訂條件 |
| `MustAsync(predicate)` | 非同步自訂條件 |
| `When(condition)` | 條件式驗證 |
| `SetValidator(validator)` | 巢狀驗證器 |

### Pipeline 執行順序

```
Request → ValidationBehavior → AuthorizationBehavior → Handler
                ↓ (失敗)
            Return Errors
```

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [02-application-layer.md](02-application-layer.md) - Application Layer（Pipeline Behavior）
- [09-error-handling-guide.md](09-error-handling-guide.md) - 錯誤處理指南
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
