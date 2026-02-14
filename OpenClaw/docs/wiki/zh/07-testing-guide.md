---
title: 測試策略指南
description: Unit Test、Integration Test 與 TestCommon 共用工具的實作指南
keywords: [Testing, Unit Test, Integration Test, xUnit, NSubstitute, FluentAssertions]
sidebar_position: 8
---

# 測試策略指南

> 學習如何為 Clean Architecture 專案撰寫完整的測試

## 概觀

Weda Template 提供完整的測試架構，包含：
- **Unit Tests**：測試 Domain 與 Application Layer 的獨立邏輯
- **Integration Tests**：測試 API 端點與完整請求流程
- **TestCommon**：共用的測試工具與 Factory

```
tests/
├── OpenClaw.Domain.UnitTests/        # Domain Layer 單元測試
├── OpenClaw.Application.UnitTests/   # Application Layer 單元測試
├── OpenClaw.Infrastructure.UnitTests/# Infrastructure Layer 單元測試
├── OpenClaw.Api.IntegrationTests/    # API 整合測試
│   └── Common/
│       ├── AppHttpClient.cs               # HTTP Client 封裝
│       └── WebApplicationFactory/
│           ├── WebAppFactory.cs           # 測試用 WebApplicationFactory
│           ├── SqliteTestDatabase.cs      # SQLite 記憶體測試資料庫
│           └── WebAppFactoryCollection.cs # xUnit Collection
└── OpenClaw.TestCommon/              # 共用測試工具
    ├── Security/
    │   ├── CurrentUserFactory.cs          # 建立測試用 CurrentUser
    │   └── TestCurrentUserProvider.cs     # 測試用 CurrentUserProvider
    └── TestUtilities/
        └── NSubstitute/Must.cs            # 自訂 Argument Matcher
```

---

## 1. 測試框架

### 1.1 使用的套件

| 套件 | 用途 |
|------|------|
| `xUnit` | 測試框架 |
| `NSubstitute` | Mock 框架 |
| `FluentAssertions` | 可讀性高的 Assertion |
| `Microsoft.AspNetCore.Mvc.Testing` | 整合測試 |

### 1.2 GlobalUsings.cs

```csharp
global using Xunit;
global using NSubstitute;
global using FluentAssertions;
global using ErrorOr;
```

---

## 2. Unit Test

### 2.1 測試 Domain Layer

測試 Entity 的建立與驗證邏輯：

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

### 2.2 測試 Value Object

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

### 2.3 測試 Application Handler

使用 NSubstitute Mock 依賴：

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

### 2.4 測試 Pipeline Behavior

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

## 3. Integration Test

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

### 3.4 Integration Test 範例

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

## 4. TestCommon 工具

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

### 4.3 自訂 Argument Matcher

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

## 5. 執行測試

### 5.1 命令列

```bash
# 執行所有測試
dotnet test

# 執行特定專案
dotnet test tests/OpenClaw.Domain.UnitTests

# 執行特定測試
dotnet test --filter "FullyQualifiedName~EmployeeTests"

# 顯示詳細輸出
dotnet test --logger "console;verbosity=detailed"

# 產生覆蓋率報告
dotnet test --collect:"XPlat Code Coverage"
```

### 5.2 VS Code

使用 `.NET Core Test Explorer` 擴充功能，可在 Test Explorer 面板執行測試。

---

## 快速參考

### 測試命名慣例

```
{MethodName}_{Scenario}_{ExpectedResult}
```

範例：
- `Create_WithValidData_ShouldReturnEmployee`
- `Handle_WhenEmailExists_ShouldReturnDuplicateError`
- `GetById_WhenNotFound_ShouldReturn404`

### 測試專案對應

| 測試專案 | 測試目標 |
|----------|----------|
| `Domain.UnitTests` | Entity、Value Object、Domain Service |
| `Application.UnitTests` | Command/Query Handler、Behavior |
| `Infrastructure.UnitTests` | Repository、External Service |
| `Api.IntegrationTests` | Controller、完整請求流程 |

### AAA Pattern

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange - 準備測試資料與相依性
    var repository = Substitute.For<IRepository>();

    // Act - 執行要測試的行為
    var result = await handler.Handle(command, default);

    // Assert - 驗證結果
    result.IsError.Should().BeFalse();
}
```

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [02-application-layer.md](02-application-layer.md) - Application Layer（Handler 實作）
- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
