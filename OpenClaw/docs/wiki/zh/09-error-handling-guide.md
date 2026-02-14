---
title: 錯誤處理指南
description: ErrorOr Pattern 與 ProblemDetails 錯誤處理的實作指南
keywords: [ErrorOr, Error Handling, ProblemDetails, Domain Errors]
sidebar_position: 10
---

# 錯誤處理指南

> 學習如何使用 ErrorOr Pattern 實作一致的錯誤處理

## 概觀

Weda Template 使用 [ErrorOr](https://github.com/amantinband/error-or) 套件實作 Railway-Oriented Programming，提供：
- 明確的錯誤型別定義
- 避免使用 Exception 控制流程
- 一致的 HTTP 回應格式（ProblemDetails）

```
src/
├── OpenClaw.Domain/
│   └── {Aggregate}/Errors/
│       └── {Aggregate}Errors.cs   # Domain 錯誤定義
├── Weda.Core/Api/
│   └── ApiController.cs           # Error 轉 HTTP Response
└── OpenClaw.Application/
    └── {Feature}/Commands/
        └── {Command}Handler.cs    # 回傳 ErrorOr<T>
```

---

## 1. ErrorOr 基礎

### 1.1 ErrorOr<T> 型別

`ErrorOr<T>` 代表「成功回傳 T」或「失敗回傳 Error 清單」：

```csharp
// 成功
ErrorOr<Employee> success = employee;

// 失敗
ErrorOr<Employee> failure = EmployeeErrors.NotFound;

// 多個錯誤
ErrorOr<Employee> failures = new List<Error>
{
    EmployeeErrors.EmptyName,
    EmployeeErrors.InvalidEmailFormat
};
```

### 1.2 Error 類型

```csharp
public enum ErrorType
{
    Failure,      // 一般失敗
    Unexpected,   // 非預期錯誤
    Validation,   // 驗證錯誤 → 400 Bad Request
    Conflict,     // 衝突錯誤 → 409 Conflict
    NotFound,     // 找不到 → 404 Not Found
    Unauthorized, // 未授權 → 403 Forbidden
    Forbidden     // 禁止存取 → 403 Forbidden
}
```

### 1.3 建立 Error

```csharp
// 使用工廠方法
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

### 2.1 定義錯誤類別

在 Domain Layer 定義 Aggregate 專屬的錯誤：

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

### 2.2 錯誤命名慣例

```
{Aggregate}.{ErrorName}
```

範例：
- `Employee.NotFound`
- `Employee.DuplicateEmail`
- `User.InvalidCredentials`
- `Order.AlreadyShipped`

---

## 3. 在 Handler 中使用

### 3.1 基本回傳

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

### 3.2 串接多個操作

```csharp
public async ValueTask<ErrorOr<EmployeeResponse>> Handle(
    CreateEmployeeCommand request,
    CancellationToken cancellationToken)
{
    // 1. 檢查 Email 是否重複
    if (await repository.ExistsWithEmailAsync(
        Email.Create(request.Email).Value, cancellationToken))
    {
        return EmployeeErrors.DuplicateEmail;
    }

    // 2. 建立 Employee（可能失敗）
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

    // 3. 儲存
    await repository.AddAsync(employeeResult.Value, cancellationToken);

    return employeeResult.Value.ToResponse();
}
```

### 3.3 使用 Match

```csharp
var result = await handler.Handle(command, cancellationToken);

return result.Match(
    success => Ok(success),
    errors => Problem(errors));
```

### 3.4 使用 Then 串接

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

## 4. API 層錯誤處理

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

### 4.2 Controller 使用

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

## 5. ProblemDetails 回應

### 5.1 Error 類型對應

| ErrorType | HTTP Status | 說明 |
|-----------|-------------|------|
| `Validation` | 400 Bad Request | 輸入驗證失敗 |
| `NotFound` | 404 Not Found | 資源不存在 |
| `Conflict` | 409 Conflict | 資源衝突（重複） |
| `Unauthorized` | 403 Forbidden | 無權限存取 |
| `Failure` | 500 Internal Server Error | 一般錯誤 |

### 5.2 回應範例

**單一錯誤 (404)**：
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "The employee with the specified ID was not found.",
  "status": 404
}
```

**驗證錯誤 (400)**：
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

## 6. Entity 中的 Factory Method

### 6.1 使用 ErrorOr 建立 Entity

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
        // 建立 Value Objects（可能失敗）
        var nameResult = EmployeeName.Create(name);
        var emailResult = Email.Create(email);
        var departmentResult = Department.Create(department);

        // 收集所有錯誤
        var errors = new List<Error>();

        if (nameResult.IsError)
            errors.AddRange(nameResult.Errors);
        if (emailResult.IsError)
            errors.AddRange(emailResult.Errors);
        if (departmentResult.IsError)
            errors.AddRange(departmentResult.Errors);

        if (errors.Count > 0)
            return errors;

        // 建立 Entity
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

## 快速參考

### 錯誤處理流程

```
Domain Error → ErrorOr<T> → Handler → Controller → ProblemDetails → HTTP Response
```

### 常用 Pattern

```csharp
// 早期返回
if (entity is null)
    return EntityErrors.NotFound;

// Match Pattern
return result.Match(
    success => Ok(success),
    errors => Problem(errors));

// Then 串接
return await CreateEntity()
    .ThenAsync(async e => await Save(e))
    .Then(e => e.ToResponse());
```

### 錯誤分類

| 類別 | 說明 | 範例 |
|------|------|------|
| Validation | 輸入格式錯誤 | `EmptyName`, `InvalidEmailFormat` |
| NotFound | 資源不存在 | `NotFound`, `SupervisorNotFound` |
| Conflict | 業務規則衝突 | `DuplicateEmail`, `AlreadyActive` |
| Unauthorized | 權限不足 | `InvalidCredentials` |

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [01-domain-layer.md](01-domain-layer.md) - Domain Layer（Entity 與 Error 定義）
- [02-application-layer.md](02-application-layer.md) - Application Layer（Handler 實作）
- [ErrorOr GitHub](https://github.com/amantinband/error-or)
- [RFC 7807 - Problem Details](https://datatracker.ietf.org/doc/html/rfc7807)
