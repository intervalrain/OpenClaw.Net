---
title: API Layer Implementation Guide
description: Step-by-step guide to implementing REST controllers and NATS event controllers
keywords: [API Layer, Controller, REST, NATS, EventController, Swagger]
sidebar_position: 5
---

# API Layer Implementation Guide

> Learn how to build REST APIs and NATS event-driven endpoints

## Overview

The API Layer provides HTTP endpoints and event-driven interfaces. It orchestrates requests to the Application Layer via Mediator.

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

## 1. REST Controllers

### 1.1 Base Controller

```csharp
// From Weda.Core
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
    /// Get all employees
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
    /// Get employee by ID
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
    /// Create a new employee
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
    /// Update an existing employee
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
    /// Delete an employee
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
    /// Get subordinates of an employee
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

### Controller Response Pattern

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

## 2. NATS Event Controllers

### 2.1 EventController Base (from Weda.Core)

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

### 2.2 Implementing Event Controller

```csharp
[Stream("employees_v1_stream")]
[Consumer("employees_v1_consumer")]
[Connection("bus")]
public class EmployeesEventController : EventController
{
    /// <summary>
    /// Request-Reply: Get employee by ID
    /// Returns a response to the requester
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
    /// JetStream Consume: Process employee creation events
    /// Default mode - continuous processing
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

        // Process the event
        await ProcessCreatedEventAsync(@event, cancellationToken);
    }

    /// <summary>
    /// JetStream Fetch: Batch process status changes
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
    /// Core Pub-Sub: Fire-and-forget notifications
    /// </summary>
    [Subject("employees.v1.notifications.>")]
    [DeliveryMode(NatsDeliveryMode.Core)]
    public void HandleNotification(EmployeeNotificationEvent @event)
    {
        Logger.LogInformation(
            "Received notification for employee: {Id}",
            @event.EmployeeId);

        // Fire-and-forget processing
    }

    private async Task ProcessCreatedEventAsync(
        CreateEmployeeNatsEvent @event,
        CancellationToken cancellationToken)
    {
        // Implementation
    }

    private async Task ProcessStatusChangeAsync(
        EmployeeStatusChangedNatsEvent @event,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### NATS Patterns Reference

| Pattern | Return Type | Attribute | Use Case |
|---------|-------------|-----------|----------|
| Request-Reply | `Task<TResponse>` | Default | Synchronous query |
| JetStream Consume | `Task` (void) | `[ConsumerMode(Consume)]` | Continuous processing |
| JetStream Fetch | `Task` (void) | `[ConsumerMode(Fetch)]` | Batch processing |
| Core Pub-Sub | `void` | `[DeliveryMode(Core)]` | Fire-and-forget |

### Subject Template Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{controller}` | Controller name | `employees` |
| `{version}` | API version | `v1` |
| `{id}` | Resource identifier | `123` |
| `>` | Wildcard (any suffix) | `notifications.>` |
| `*` | Single token wildcard | `*.created` |

---

## 3. Contracts

### 3.1 Request DTOs

```csharp
// In Contracts layer
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

### 3.2 Response DTOs

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

### 3.3 Swagger Examples

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

## 4. API Mappers

### 4.1 Mapperly for API Layer

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

### Mapping Flow

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

## 5. Program.cs Configuration

### 5.1 Application Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add layers
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Add Weda.Core with all features
builder.Services.AddWedaCore(builder.Configuration, options =>
{
    options.AddMediator(typeof(IApplicationMarker).Assembly);
    options.AddValidators(typeof(IApplicationMarker).Assembly);
    options.AddSwagger();
    options.AddAuthentication();
    options.AddNats();
});

// Add API versioning
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

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Initialize database
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
    /// Generate authentication token
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetToken([FromBody] GetAuthRequest request)
    {
        // In production, validate credentials against user store
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
// Configured in Weda.Core
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

## Quick Reference

### File Naming Conventions

| Component | Pattern | Example |
|-----------|---------|---------|
| Controller | `{Resource}Controller.cs` | `EmployeesController.cs` |
| Event Controller | `{Resource}EventController.cs` | `EmployeesEventController.cs` |
| Request | `{Action}{Resource}Request.cs` | `CreateEmployeeRequest.cs` |
| Response | `{Resource}Response.cs` | `EmployeeResponse.cs` |
| Mapper | `{Resource}Mapper.cs` | `EmployeeMapper.cs` |
| Module | `{Project}ApiModule.cs` | `WedaTemplateApiModule.cs` |

### Folder Structure

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

### HTTP Status Codes

| Code | Meaning | When to Use |
|------|---------|-------------|
| 200 | OK | Successful GET, PUT |
| 201 | Created | Successful POST |
| 204 | No Content | Successful DELETE |
| 400 | Bad Request | Validation errors |
| 401 | Unauthorized | Missing/invalid auth |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Duplicate or state conflict |
| 500 | Server Error | Unexpected errors |

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer Guide
- [ASP.NET Core Documentation](https://learn.microsoft.com/aspnet/core/)
- [NATS Documentation](https://docs.nats.io/)
