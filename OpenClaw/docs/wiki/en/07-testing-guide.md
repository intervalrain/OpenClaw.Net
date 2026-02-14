---
title: Testing Strategy Guide
description: Implementation guide for Unit Tests, Integration Tests, and TestCommon utilities
keywords: [Testing, Unit Test, Integration Test, xUnit, NSubstitute, FluentAssertions]
sidebar_position: 8
---

# Testing Strategy Guide

> Learn how to write comprehensive tests for Clean Architecture projects

## Overview

Weda Template provides a complete testing architecture, including:
- **Unit Tests**: Test isolated logic in Domain and Application layers
- **Integration Tests**: Test API endpoints and complete request flows
- **TestCommon**: Shared test utilities and factories

```
tests/
├── OpenClaw.Domain.UnitTests/        # Domain Layer unit tests
├── OpenClaw.Application.UnitTests/   # Application Layer unit tests
├── OpenClaw.Infrastructure.UnitTests/# Infrastructure Layer unit tests
├── OpenClaw.Api.IntegrationTests/    # API integration tests
│   └── Common/
│       ├── AppHttpClient.cs               # HTTP Client wrapper
│       └── WebApplicationFactory/
│           ├── WebAppFactory.cs           # Test WebApplicationFactory
│           ├── SqliteTestDatabase.cs      # SQLite in-memory test database
│           └── WebAppFactoryCollection.cs # xUnit Collection
└── OpenClaw.TestCommon/              # Shared test utilities
    ├── Security/
    │   ├── CurrentUserFactory.cs          # Create test CurrentUser
    │   └── TestCurrentUserProvider.cs     # Test CurrentUserProvider
    └── TestUtilities/
        └── NSubstitute/Must.cs            # Custom argument matchers
```

---

## 1. Testing Framework

### 1.1 Packages Used

| Package | Purpose |
|---------|---------|
| `xUnit` | Test framework |
| `NSubstitute` | Mocking framework |
| `FluentAssertions` | Readable assertions |
| `Microsoft.AspNetCore.Mvc.Testing` | Integration testing |

### 1.2 GlobalUsings.cs

```csharp
global using Xunit;
global using NSubstitute;
global using FluentAssertions;
global using ErrorOr;
```

---

## 2. Unit Tests

### 2.1 Testing Domain Layer

Test entity creation and validation logic:

```csharp
public class EmployeeTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnEmployee()
    {
        // Arrange
        var name = "John Doe";
        var email = "john@example.com";
        var department = "Engineering";
        var position = "Developer";
        var hireDate = DateTime.UtcNow;

        // Act
        var result = Employee.Create(
            name, email, department, position, hireDate);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Value.Should().Be(name);
        result.Value.Email.Value.Should().Be(email);
        result.Value.Status.Should().Be(EmployeeStatus.Active);
    }

    [Fact]
    public void Create_WithInvalidEmail_ShouldReturnError()
    {
        // Arrange
        var invalidEmail = "invalid-email";

        // Act
        var result = Employee.Create(
            "John", invalidEmail, "Dept", "Position", DateTime.UtcNow);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }
}
```

### 2.2 Testing Value Objects

```csharp
public class EmailTests
{
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.org")]
    public void Create_WithValidEmail_ShouldSucceed(string email)
    {
        // Act
        var result = Email.Create(email);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Value.Should().Be(email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("@domain.com")]
    public void Create_WithInvalidEmail_ShouldFail(string email)
    {
        // Act
        var result = Email.Create(email);

        // Assert
        result.IsError.Should().BeTrue();
    }
}
```

### 2.3 Testing Application Handlers

Using NSubstitute to mock dependencies:

```csharp
public class CreateEmployeeCommandHandlerTests
{
    private readonly IEmployeeRepository _mockRepository;
    private readonly CreateEmployeeCommandHandler _handler;

    public CreateEmployeeCommandHandlerTests()
    {
        _mockRepository = Substitute.For<IEmployeeRepository>();
        _handler = new CreateEmployeeCommandHandler(_mockRepository);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateEmployee()
    {
        // Arrange
        var command = new CreateEmployeeCommand(
            "John Doe",
            "john@example.com",
            "Engineering",
            "Developer",
            DateTime.UtcNow);

        _mockRepository
            .ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _mockRepository.Received(1).AddAsync(
            Arg.Any<Employee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldReturnError()
    {
        // Arrange
        var command = new CreateEmployeeCommand(
            "John Doe",
            "existing@example.com",
            "Engineering",
            "Developer",
            DateTime.UtcNow);

        _mockRepository
            .ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(EmployeeErrors.DuplicateEmail);
    }
}
```

### 2.4 Testing Pipeline Behaviors

```csharp
public class AuthorizationBehaviorTests
{
    private readonly IAuthorizationService _mockAuthorizationService;

    public AuthorizationBehaviorTests()
    {
        _mockAuthorizationService = Substitute.For<IAuthorizationService>();
    }

    [Fact]
    public async Task Handle_WhenNoAuthorizationAttribute_ShouldInvokeNext()
    {
        // Arrange
        var request = new RequestWithNoAuthorization(Guid.NewGuid());
        var behavior = new AuthorizationBehavior<
            RequestWithNoAuthorization, ErrorOr<Response>>(
            _mockAuthorizationService);

        MessageHandlerDelegate<RequestWithNoAuthorization, ErrorOr<Response>> next =
            (_, _) => new ValueTask<ErrorOr<Response>>(Response.Instance);

        // Act
        var result = await behavior.Handle(request, next, default);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthorized_ShouldReturnError()
    {
        // Arrange
        var request = new RequestWithAuthorization(Guid.NewGuid());
        var error = Error.Unauthorized();

        _mockAuthorizationService
            .AuthorizeCurrentUser(
                request,
                Must.BeEmptyList<string>(),
                Must.BeListWith(["Permission"]),
                Must.BeEmptyList<string>())
            .Returns(error);

        var behavior = new AuthorizationBehavior<
            RequestWithAuthorization, ErrorOr<Response>>(
            _mockAuthorizationService);

        MessageHandlerDelegate<RequestWithAuthorization, ErrorOr<Response>> next =
            (_, _) => new ValueTask<ErrorOr<Response>>(Response.Instance);

        // Act
        var result = await behavior.Handle(request, next, default);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(error);
    }
}
```

---

## 3. Integration Tests

### 3.1 WebAppFactory

```csharp
public class WebAppFactory : WebApplicationFactory<IAssemblyMarker>, IAsyncLifetime
{
    private SqliteTestDatabase _testDatabase = null!;

    public AppHttpClient CreateAppHttpClient()
    {
        return new AppHttpClient(CreateClient());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync()
    {
        _testDatabase.Dispose();
        return Task.CompletedTask;
    }

    public void ResetDatabase()
    {
        _testDatabase.ResetDatabase();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _testDatabase = SqliteTestDatabase.CreateAndInitialize();

        builder.ConfigureTestServices(services => services
            .RemoveAll<DbContextOptions<AppDbContext>>()
            .AddDbContext<AppDbContext>((sp, options) =>
                options.UseSqlite(_testDatabase.Connection)));

        builder.ConfigureAppConfiguration((context, conf) =>
            conf.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailSettings:EnableEmailNotifications", "false" },
            }));
    }
}
```

### 3.2 SqliteTestDatabase

```csharp
public class SqliteTestDatabase : IDisposable
{
    public SqliteConnection Connection { get; }

    public static SqliteTestDatabase CreateAndInitialize()
    {
        var testDatabase = new SqliteTestDatabase("DataSource=:memory:");
        testDatabase.InitializeDatabase();
        return testDatabase;
    }

    public void InitializeDatabase()
    {
        Connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(Connection)
            .Options;

        using var context = new AppDbContext(options, null!, null!);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }

    public void ResetDatabase()
    {
        Connection.Close();
        InitializeDatabase();
    }

    public void Dispose() => Connection.Close();

    private SqliteTestDatabase(string connectionString)
    {
        Connection = new SqliteConnection(connectionString);
    }
}
```

### 3.3 AppHttpClient

```csharp
public class AppHttpClient(HttpClient _httpClient)
{
    public HttpClient HttpClient => _httpClient;

    public void SetBearerToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
```

### 3.4 Integration Test Example

```csharp
[Collection(nameof(WebAppFactoryCollection))]
public class EmployeesControllerTests
{
    private readonly AppHttpClient _client;
    private readonly WebAppFactory _factory;

    public EmployeesControllerTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAppHttpClient();
        factory.ResetDatabase();
    }

    [Fact]
    public async Task CreateEmployee_WithValidData_ShouldReturn201()
    {
        // Arrange
        var request = new CreateEmployeeRequest(
            "John Doe",
            "john@example.com",
            "Engineering",
            "Developer",
            DateTime.UtcNow);

        // Act
        var response = await _client.HttpClient.PostAsJsonAsync(
            "/api/v1/employees", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var employee = await response.Content
            .ReadFromJsonAsync<EmployeeResponse>();
        employee.Should().NotBeNull();
        employee!.Name.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetEmployee_WhenNotExists_ShouldReturn404()
    {
        // Act
        var response = await _client.HttpClient.GetAsync(
            "/api/v1/employees/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

### 3.5 xUnit Collection

```csharp
[CollectionDefinition(nameof(WebAppFactoryCollection))]
public class WebAppFactoryCollection : ICollectionFixture<WebAppFactory>
{
}
```

---

## 4. TestCommon Utilities

### 4.1 CurrentUserFactory

```csharp
public static class CurrentUserFactory
{
    public static CurrentUser CreateCurrentUser(
        Guid? id = null,
        string name = "Test User",
        string email = "test@example.com",
        IReadOnlyList<string>? permissions = null,
        IReadOnlyList<string>? roles = null)
    {
        return new CurrentUser(
            id ?? Guid.NewGuid(),
            name,
            email,
            permissions ?? [],
            roles ?? []);
    }
}
```

### 4.2 TestCurrentUserProvider

```csharp
public class TestCurrentUserProvider : ICurrentUserProvider
{
    private CurrentUser? _currentUser;

    public void SetCurrentUser(CurrentUser user)
    {
        _currentUser = user;
    }

    public CurrentUser GetCurrentUser()
    {
        return _currentUser ?? throw new InvalidOperationException(
            "Current user not set. Call SetCurrentUser first.");
    }
}
```

### 4.3 Custom Argument Matchers

```csharp
public static class Must
{
    public static List<T> BeEmptyList<T>()
    {
        return Arg.Is<List<T>>(list => list.Count == 0);
    }

    public static List<T> BeListWith<T>(T[] expectedItems)
    {
        return Arg.Is<List<T>>(list =>
            list.Count == expectedItems.Length &&
            expectedItems.All(item => list.Contains(item)));
    }
}
```

---

## 5. Running Tests

### 5.1 Command Line

```bash
# Run all tests
dotnet test

# Run specific project
dotnet test tests/OpenClaw.Domain.UnitTests

# Run specific test
dotnet test --filter "FullyQualifiedName~EmployeeTests"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### 5.2 VS Code

Use the `.NET Core Test Explorer` extension to run tests from the Test Explorer panel.

---

## Quick Reference

### Test Naming Convention

```
{MethodName}_{Scenario}_{ExpectedResult}
```

Examples:
- `Create_WithValidData_ShouldReturnEmployee`
- `Handle_WhenEmailExists_ShouldReturnDuplicateError`
- `GetById_WhenNotFound_ShouldReturn404`

### Test Project Mapping

| Test Project | Test Target |
|--------------|-------------|
| `Domain.UnitTests` | Entity, Value Object, Domain Service |
| `Application.UnitTests` | Command/Query Handler, Behavior |
| `Infrastructure.UnitTests` | Repository, External Service |
| `Api.IntegrationTests` | Controller, complete request flow |

### AAA Pattern

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange - Prepare test data and dependencies
    var repository = Substitute.For<IRepository>();

    // Act - Execute the behavior under test
    var result = await handler.Handle(command, default);

    // Assert - Verify the result
    result.IsError.Should().BeFalse();
}
```

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [02-application-layer.md](02-application-layer.md) - Application Layer (Handler Implementation)
- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
