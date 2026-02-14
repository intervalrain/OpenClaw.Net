---
title: API Layer 實作指南
description: REST Controller 與 NATS Event Controller 的逐步實作指南
keywords: [API Layer, Controller, REST, NATS, EventController, Swagger]
sidebar_position: 5
---

# API Layer 實作指南

> 學習如何建構 REST API 與 NATS Event-Driven Endpoint

## 概觀

API Layer 提供 HTTP Endpoint 與 Event-Driven Interface，透過 Mediator 將請求協調至 Application Layer。

```
src/OpenClaw.Api/
├── Controllers/
│   ├── ApiController.cs
│   ├── EmployeesController.cs
│   └── AuthController.cs
├── EventControllers/
│   └── EmployeesEventController.cs
├── Mapping/
│   └── EmployeeMapper.cs
├── Program.cs
├── IAssemblyMarker.cs
└── WedaTemplateApiModule.cs
```

---

## 1. REST Controller

### 1.1 Base Controller

```csharp
// 來自 Weda.Core
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public class ApiController : ControllerBase
{
    protected ActionResult Problem(List<Error> errors)
    {
        if (errors.Count == 0)
            return Problem();

        if (errors.All(e => e.Type == ErrorType.Validation))
            return ValidationProblem(errors);

        return Problem(errors[0]);
    }

    private ActionResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description);
    }

    private ActionResult ValidationProblem(List<Error> errors)
    {
        var modelState = new ModelStateDictionary();

        foreach (var error in errors)
        {
            modelState.AddModelError(error.Code, error.Description);
        }

        return ValidationProblem(modelState);
    }
}
```

### 1.2 Resource Controller

```csharp
[ApiVersion("1.0")]
public class EmployeesController : ApiController
{
    private readonly ISender _mediator;

    public EmployeesController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 取得所有員工
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<EmployeeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEmployees(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListEmployeesQuery(),
            cancellationToken);

        return result.Match(
            employees => Ok(employees.Select(EmployeeMapper.ToResponse)),
            Problem);
    }

    /// <summary>
    /// 依 ID 取得員工
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployee(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetEmployeeQuery(id),
            cancellationToken);

        return result.Match(
            employee => Ok(EmployeeMapper.ToResponse(employee)),
            Problem);
    }

    /// <summary>
    /// 建立新員工
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeCommand(
            request.Name,
            request.Email,
            request.Department,
            request.Position,
            request.HireDate,
            request.SupervisorId);

        var result = await _mediator.Send(command, cancellationToken);

        return result.Match(
            employee => CreatedAtAction(
                nameof(GetEmployee),
                new { id = employee.Id },
                EmployeeMapper.ToResponse(employee)),
            Problem);
    }

    /// <summary>
    /// 更新現有員工
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmployee(
        int id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeCommand(
            id,
            request.Name,
            request.Department,
            request.Position);

        var result = await _mediator.Send(command, cancellationToken);

        return result.Match(
            employee => Ok(EmployeeMapper.ToResponse(employee)),
            Problem);
    }

    /// <summary>
    /// 刪除員工
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEmployee(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new DeleteEmployeeCommand(id),
            cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    /// <summary>
    /// 取得員工的下屬
    /// </summary>
    [HttpGet("{id:int}/subordinates")]
    [ProducesResponseType(typeof(List<EmployeeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubordinates(
        int id,
        [FromQuery] bool includeIndirect = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetSubordinatesQuery(id, includeIndirect),
            cancellationToken);

        return result.Match(
            subordinates => Ok(subordinates.Select(EmployeeMapper.ToResponse)),
            Problem);
    }
}
```

### Controller Response 模式

```
┌────────────────────────────────────────────────────────────────────┐
│                       Controller Response Flow                     │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  HTTP Request                                                      │
│       ↓                                                            │
│  Map Request → Command/Query                                       │
│       ↓                                                            │
│  Send via Mediator                                                 │
│       ↓                                                            │
│  Receive ErrorOr<T>                                                │
│       ↓                                                            │
│  result.Match(                                                     │
│      success => Ok/Created/NoContent,                              │
│      errors => Problem                                             │
│  )                                                                 │
│       ↓                                                            │
│  HTTP Response                                                     │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## 2. NATS Event Controller

### 2.1 EventController Base（來自 Weda.Core）

```csharp
[Stream("stream_name")]
[Consumer("consumer_name")]
[Connection("bus")]
public abstract class EventController
{
    public IMediator Mediator { get; internal set; }
    public INatsConnectionProvider NatsProvider { get; internal set; }
    public ILogger Logger { get; internal set; }
    public string Subject { get; internal set; }
    public IReadOnlyDictionary<string, string> SubjectValues { get; internal set; }
}
```

### 2.2 實作 Event Controller

```csharp
[Stream("employees_v1_stream")]
[Consumer("employees_v1_consumer")]
[Connection("bus")]
public class EmployeesEventController : EventController
{
    /// <summary>
    /// Request-Reply：依 ID 取得員工
    /// 回傳回應給請求者
    /// </summary>
    [Subject("employees.v1.{id}.get")]
    public async Task<GetEmployeeResponse> GetEmployee(
        GetEmployeeNatsRequest request,
        CancellationToken cancellationToken)
    {
        var id = int.Parse(SubjectValues["id"]);

        var result = await Mediator.Send(
            new GetEmployeeQuery(id),
            cancellationToken);

        if (result.IsError)
        {
            return new GetEmployeeResponse(
                Success: false,
                Employee: null,
                Error: result.Errors.First().Description);
        }

        return new GetEmployeeResponse(
            Success: true,
            Employee: result.Value,
            Error: null);
    }

    /// <summary>
    /// JetStream Consume：處理員工建立事件
    /// 預設模式 - 持續處理
    /// </summary>
    [Subject("employees.v1.created")]
    public async Task HandleEmployeeCreated(
        CreateEmployeeNatsEvent @event,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "Processing employee created event: {Id} - {Name}",
            @event.Id,
            @event.Name);

        // 處理事件
        await ProcessCreatedEventAsync(@event, cancellationToken);
    }

    /// <summary>
    /// JetStream Fetch：批次處理狀態變更
    /// </summary>
    [Subject("employees.v1.status-changed")]
    [ConsumerMode(NatsConsumerMode.Fetch)]
    public async Task HandleStatusChanged(
        EmployeeStatusChangedNatsEvent @event,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "Processing status change: {Id} {OldStatus} -> {NewStatus}",
            @event.EmployeeId,
            @event.OldStatus,
            @event.NewStatus);

        await ProcessStatusChangeAsync(@event, cancellationToken);
    }

    /// <summary>
    /// Core Pub-Sub：Fire-and-Forget 通知
    /// </summary>
    [Subject("employees.v1.notifications.>")]
    [DeliveryMode(NatsDeliveryMode.Core)]
    public void HandleNotification(EmployeeNotificationEvent @event)
    {
        Logger.LogInformation(
            "Received notification for employee: {Id}",
            @event.EmployeeId);

        // Fire-and-Forget 處理
    }

    private async Task ProcessCreatedEventAsync(
        CreateEmployeeNatsEvent @event,
        CancellationToken cancellationToken)
    {
        // 實作
    }

    private async Task ProcessStatusChangeAsync(
        EmployeeStatusChangedNatsEvent @event,
        CancellationToken cancellationToken)
    {
        // 實作
    }
}
```

### NATS Pattern 參考

| Pattern | 回傳類型 | Attribute | 使用情境 |
|---------|----------|-----------|----------|
| Request-Reply | `Task<TResponse>` | 預設 | 同步查詢 |
| JetStream Consume | `Task`（void） | `[ConsumerMode(Consume)]` | 持續處理 |
| JetStream Fetch | `Task`（void） | `[ConsumerMode(Fetch)]` | 批次處理 |
| Core Pub-Sub | `void` | `[DeliveryMode(Core)]` | Fire-and-Forget |

### Subject Template 變數

| 變數 | 說明 | 範例 |
|------|------|------|
| `{controller}` | Controller 名稱 | `employees` |
| `{version}` | API 版本 | `v1` |
| `{id}` | 資源識別碼 | `123` |
| `>` | Wildcard（任意後綴） | `notifications.>` |
| `*` | 單一 Token Wildcard | `*.created` |

---

## 3. Contract

### 3.1 Request DTO

```csharp
// 在 Contracts Layer
public record CreateEmployeeRequest(
    string Name,
    string Email,
    string Department,
    string Position,
    DateTime HireDate,
    int? SupervisorId);

public record UpdateEmployeeRequest(
    string Name,
    string Department,
    string Position);
```

### 3.2 Response DTO

```csharp
public record EmployeeResponse(
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

### 3.3 Swagger 範例

```csharp
public class CreateEmployeeRequestExample
    : IExamplesProvider<CreateEmployeeRequest>
{
    public CreateEmployeeRequest GetExamples()
    {
        return new CreateEmployeeRequest(
            Name: "John Doe",
            Email: "john.doe@example.com",
            Department: "Engineering",
            Position: "Software Engineer",
            HireDate: DateTime.UtcNow,
            SupervisorId: null);
    }
}
```

---

## 4. API Mapper

### 4.1 用於 API Layer 的 Mapperly

```csharp
[Mapper]
public static partial class EmployeeMapper
{
    public static partial EmployeeResponse ToResponse(EmployeeDto dto);

    public static IEnumerable<EmployeeResponse> ToResponses(
        IEnumerable<EmployeeDto> dtos)
    {
        return dtos.Select(ToResponse);
    }
}
```

### Mapping 流程

```
Domain Entity
     ↓ (Application Mapper)
EmployeeDto
     ↓ (API Mapper)
EmployeeResponse
     ↓
JSON Response
```

---

## 5. Program.cs 設定

### 5.1 Application 設定

```csharp
var builder = WebApplication.CreateBuilder(args);

// 加入各 Layer
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// 加入 Weda.Core 及所有功能
builder.Services.AddWedaCore(builder.Configuration, options =>
{
    options.AddMediator(typeof(IApplicationMarker).Assembly);
    options.AddValidators(typeof(IApplicationMarker).Assembly);
    options.AddSwagger();
    options.AddAuthentication();
    options.AddNats();
});

// 加入 API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();

// 設定 Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 初始化資料庫
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.Run();
```

### 5.2 API Module

```csharp
public static class WedaTemplateApiModule
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }
}
```

---

## 6. Authentication Controller

### 6.1 Auth Endpoint

```csharp
[ApiVersion("1.0")]
[AllowAnonymous]
public class AuthController : ApiController
{
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthController(IJwtTokenGenerator tokenGenerator)
    {
        _tokenGenerator = tokenGenerator;
    }

    /// <summary>
    /// 產生驗證 Token
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetToken([FromBody] GetAuthRequest request)
    {
        // 正式環境應驗證憑證
        if (string.IsNullOrEmpty(request.Email))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials");
        }

        var token = _tokenGenerator.GenerateToken(
            userId: 1,
            email: request.Email,
            roles: ["User"],
            permissions: ["employees:read", "employees:write"]);

        return Ok(new AuthResponse(token));
    }
}
```

---

## 7. Error Handling

### 7.1 Global Exception Handler

```csharp
// 在 Weda.Core 中設定
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var error = context.Features.Get<IExceptionHandlerFeature>();

        if (error != null)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = error.Error.Message
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    });
});
```

### 7.2 ProblemDetails Response

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Employee.NotFound",
  "status": 404,
  "detail": "Employee not found",
  "traceId": "00-abc123-def456-00"
}
```

---

## 快速參考

### 檔案命名慣例

| 元件 | 模式 | 範例 |
|------|------|------|
| Controller | `{Resource}Controller.cs` | `EmployeesController.cs` |
| Event Controller | `{Resource}EventController.cs` | `EmployeesEventController.cs` |
| Request | `{Action}{Resource}Request.cs` | `CreateEmployeeRequest.cs` |
| Response | `{Resource}Response.cs` | `EmployeeResponse.cs` |
| Mapper | `{Resource}Mapper.cs` | `EmployeeMapper.cs` |
| Module | `{Project}ApiModule.cs` | `WedaTemplateApiModule.cs` |

### 資料夾結構

```
Api/
├── Controllers/
│   ├── ApiController.cs
│   └── {Resource}Controller.cs
├── EventControllers/
│   └── {Resource}EventController.cs
├── Mapping/
│   └── {Resource}Mapper.cs
├── Program.cs
├── IAssemblyMarker.cs
└── {Project}ApiModule.cs
```

### HTTP Status Code

| Code | 意義 | 使用時機 |
|------|------|----------|
| 200 | OK | 成功的 GET、PUT |
| 201 | Created | 成功的 POST |
| 204 | No Content | 成功的 DELETE |
| 400 | Bad Request | 驗證錯誤 |
| 401 | Unauthorized | 缺少/無效的驗證 |
| 403 | Forbidden | 權限不足 |
| 404 | Not Found | 資源不存在 |
| 409 | Conflict | 重複或狀態衝突 |
| 500 | Server Error | 非預期錯誤 |

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer 指南
- [ASP.NET Core Documentation](https://learn.microsoft.com/aspnet/core/)
- [NATS Documentation](https://docs.nats.io/)
