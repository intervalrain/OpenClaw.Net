# WEDA Template

[English](README.md)

一個適用於 .NET 10 應用程式的生產級 Clean Architecture 模板，採用領域驅動設計 (DDD)、CQRS 模式，並包含完整的測試實踐。

## 功能特色

- **Clean Architecture** - 分層架構，關注點分離清晰
- **領域驅動設計 (DDD)** - Entity、Value Object、Aggregate Root、Domain Event
- **CQRS 模式** - 使用 Mediator 實現命令查詢職責分離
- **多資料庫支援** - SQLite、PostgreSQL、MongoDB
- **JWT 認證** - 基於角色和權限的授權機制
- **NATS 訊息傳遞** - 支援 request-reply 和 pub-sub 模式的事件驅動架構
- **API 版本控制** - 內建 API 版本控制支援
- **Swagger/OpenAPI** - 自動產生 API 文件
- **完整測試** - 單元測試、整合測試、子表層測試
- **分散式快取** - 基於 NATS KV 的分散式快取，實作 IDistributedCache
- **物件儲存** - 使用 NATS Object Store 的二進位檔案儲存
- **SAGA 模式** - 分散式交易編排與補償機制
- **可觀測性** - OpenTelemetry 追蹤與指標

## 預覽

### 開發者 UI
+ 適用於開發者環境的 UI，包含 Swagger UI, Wedally UI 與 Wiki
![homepage](resources/homepage.png)

### 預設置的 Swagger UI
+ 預先配置好的 Swagger UI，包含 Grouping, Tags, SecurityRequirement 設定
![swagger](resources/swagger.png)

###  NATS 端點 UI (Wedally UI)
+ 類似 NATS 版的 swagger UI，支援直接操作，並提供可直接執行的 payload
![wedally](resources/wedally.png)
+ 直接提供操作
![wedally_req](resources/wedally_req.png)

### 自動產生的 wiki page
+ 自動將 `docs/wiki/{en,zh}` 路徑下的文章轉成靜態網頁
+ 支援 markdown 格式渲染
![wiki](resources/wiki.png)


## 專案結構

```
WedaTemplate/
├── src/
│   ├── Weda.Core/                    # 共用基礎設施 (DDD, CQRS, NATS, Cache, SAGA)
│   ├── OpenClaw.Api/            # REST API 層
│   ├── OpenClaw.Application/    # 應用層/CQRS 層
│   ├── OpenClaw.Contracts/      # DTO 和契約
│   ├── OpenClaw.Domain/         # 領域層
│   └── OpenClaw.Infrastructure/ # 基礎設施層
├── tests/
│   ├── OpenClaw.Api.IntegrationTests/
│   ├── OpenClaw.Application.UnitTests/
│   ├── OpenClaw.Domain.UnitTests/
│   ├── OpenClaw.Infrastructure.UnitTests/
│   └── OpenClaw.TestCommon/
└── tools/
    └── WikiGenerator/
```

## 快速開始

### 先決條件

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker & Docker Compose（選用）
- NATS Server（選用，用於訊息傳遞功能）

### 使用 .NET CLI 執行

```bash
dotnet run --project src/OpenClaw.Api
```

### 使用 Docker Compose 執行

```bash
docker compose up
```

API 將在 `http://localhost:5001` 上提供服務

### 存取 Swagger UI

瀏覽 `http://localhost:5001/swagger` 來查看 API 文件。

## 領域模型

### Employee（員工）

- 階層式組織架構，支援主管關係
- 部門管理（Engineering、HR、Finance、Marketing、Sales、Operations）
- 狀態追蹤（Active、OnLeave、Inactive）
- 下屬管理，防止循環參照

### User（使用者）

- 基於 Email 的認證
- 角色管理（User、Admin、SuperAdmin）
- 基於權限的存取控制
- 登入追蹤

## API 端點

### Auth（認證）

| 方法 | 端點 | 說明 |
|------|------|------|
| POST | `/api/v1/auth/login` | 使用者登入 |

### Users（使用者）

| 方法 | 端點 | 角色 | 說明 |
|------|------|------|------|
| GET | `/api/v1/users/me` | 已認證 | 取得目前使用者 |
| GET | `/api/v1/users` | Admin | 列出所有使用者 |
| GET | `/api/v1/users/{id}` | Admin | 依 ID 取得使用者 |
| POST | `/api/v1/users` | Admin | 建立使用者 |
| PUT | `/api/v1/users/{id}` | Admin | 更新使用者 |
| PUT | `/api/v1/users/{id}/roles` | SuperAdmin | 更新角色 |
| DELETE | `/api/v1/users/{id}` | Admin | 刪除使用者 |

### Employees（員工）

| 方法 | 端點 | 說明 |
|------|------|------|
| GET | `/api/v1/employees` | 列出所有員工 |
| GET | `/api/v1/employees/{id}` | 依 ID 取得員工 |
| POST | `/api/v1/employees` | 建立員工 |
| PUT | `/api/v1/employees/{id}` | 更新員工 |
| DELETE | `/api/v1/employees/{id}` | 刪除員工 |
| GET | `/api/v1/employees/{id}/subordinates` | 取得下屬 |

## NATS 整合

### EventController - 類似 ApiController 的 NATS 開發體驗

此模板提供 `EventController`，一個類似 ASP.NET Core `ApiController` 的抽象層，讓你可以用熟悉的模式處理 NATS 訊息。

```csharp
[ApiVersion("1")]
public class EmployeeEventController : EventController
{
    // Request-Reply 模式，支援 subject 路由
    [Subject("[controller].v{version:apiVersion}.{id}.get")]
    public async Task<GetEmployeeResponse> GetEmployee(int id)
    {
        var query = new GetEmployeeQuery(id);
        var result = await Mediator.Send(query);
        return new GetEmployeeResponse(result.Value);
    }

    // 建立員工
    [Subject("[controller].v{version:apiVersion}.create")]
    public async Task<GetEmployeeResponse> CreateEmployee(CreateEmployeeRequest request)
    {
        var command = new CreateEmployeeCommand(request.Name, request.Email, ...);
        var result = await Mediator.Send(command);
        return new GetEmployeeResponse(result.Value);
    }

    // JetStream Consume 模式（fire-and-forget）
    [Subject("[controller].v{version:apiVersion}.created")]
    public async Task OnEmployeeCreated(CreateEmployeeNatsEvent @event)
    {
        var command = new CreateEmployeeCommand(@event.Name, @event.Email, ...);
        await Mediator.Send(command);
    }
}
```

### 支援的 NATS 模式

| 模式 | 說明 | 使用場景 |
|------|------|----------|
| **Request-Reply** | 同步請求與回應 | CRUD 操作 |
| **JetStream Consume** | 持續訊息處理 | 事件處理器 |
| **JetStream Fetch** | 批次訊息處理 | 批量操作 |
| **Core Pub-Sub** | Fire-and-forget 訊息 | 通知 |

### NATS 端點

| Subject | 說明 |
|---------|------|
| `employee.v1.{id}.get` | 依 ID 取得員工 |
| `employee.v1.getAll` | 列出所有員工 |
| `employee.v1.create` | 建立員工 |
| `employee.v1.{id}.update` | 更新員工 |
| `employee.v1.{id}.delete` | 刪除員工 |

### NATS CLI 範例

複製貼上以下指令即可直接操作 NATS 端點：

**依 ID 取得員工：**
```bash
nats req employee.v1.1.get ''
```

**列出所有員工：**
```bash
nats req employee.v1.getAll ''
```

**建立員工：**
```bash
nats req employee.v1.create '{"name":"John Doe","email":"john@example.com","department":"Engineering","position":"Software Engineer"}'
```

**更新員工：**
```bash
nats req employee.v1.1.update '{"name":"John Doe","email":"john.doe@example.com","department":"Engineering","position":"Senior Engineer","status":"Active"}'
```

**刪除員工：**
```bash
nats req employee.v1.1.delete ''
```

## 設定

### 資料庫

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=OpenClaw.sqlite"
  }
}
```

支援的 Provider：`Sqlite`、`PostgreSQL`、`MongoDB`

### JWT 設定

```json
{
  "JwtSettings": {
    "Secret": "your-secret-key-at-least-32-characters",
    "TokenExpirationInMinutes": 60,
    "Issuer": "WedaTemplate",
    "Audience": "WedaTemplate"
  }
}
```

### NATS 訊息傳遞

```json
{
  "Nats": {
    "Url": "nats://localhost:4222",
    "Name": "openclaw"
  }
}
```

### Email 通知

```json
{
  "EmailSettings": {
    "EnableEmailNotifications": false,
    "DefaultFromEmail": "your-email@example.com",
    "SmtpSettings": {
      "Server": "smtp.gmail.com",
      "Port": 587,
      "Username": "your-email@gmail.com",
      "Password": "your-password"
    }
  }
}
```

## 授權機制

此模板支援三種授權類型：

### 基於角色的授權

```csharp
[Authorize(Roles = "Admin")]
public record GetUserQuery(Guid Id) : IAuthorizeableRequest<ErrorOr<User>>;
```

### 基於權限的授權

```csharp
[Authorize(Permissions = "users:read")]
public record ListUsersQuery : IAuthorizeableRequest<ErrorOr<List<User>>>;
```

### 基於策略的授權

```csharp
[Authorize(Policies = "SelfOrAdmin")]
public record UpdateUserCommand(Guid Id, ...) : IAuthorizeableRequest<ErrorOr<User>>;
```

## 測試

```bash
# 執行所有測試
dotnet test

# 執行並產生覆蓋率報告
dotnet test --collect:"XPlat Code Coverage"
```

### 測試類型

- **領域單元測試** - 測試領域實體和值物件
- **應用層單元測試** - 測試處理器和管線行為
- **基礎設施單元測試** - 測試儲存庫和持久化
- **整合測試** - 端對端 API 測試

## 架構模式

| 模式 | 實作 |
|------|------|
| 領域驅動設計 | Entity、AggregateRoot、Value Object、Domain Event |
| CQRS | 基於 Mediator 的命令/查詢分離 |
| Repository 模式 | 通用和特定儲存庫 |
| Pipeline Behavior | 驗證和授權的橫切關注點 |
| 最終一致性 | 基於 Middleware 的領域事件發布 |
| 事件驅動 | NATS 訊息傳遞用於非同步通訊 |
| 分散式快取 | NATS KV 搭配 IDistributedCache 介面 |
| 物件儲存 | NATS Object Store 用於二進位檔案 |
| SAGA 模式 | 編排式分散式交易 |
| 可觀測性 | OpenTelemetry 追蹤與指標 |

## Weda.Core 基礎設施

`Weda.Core` 函式庫提供生產級的基礎設施模式：

### 分散式快取 (NATS KV)

```csharp
// 注入 IDistributedCache
public class MyService(IDistributedCache cache)
{
    public async Task CacheDataAsync(string key, MyData data)
    {
        var json = JsonSerializer.Serialize(data);
        await cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        });
    }
}
```

### 物件儲存（二進位檔案）

```csharp
// 注入 IBlobStorage
public class FileService(IBlobStorage storage)
{
    public async Task<string> UploadAsync(Stream file, string filename)
    {
        return await storage.UploadAsync(file, filename);
    }

    public async Task<Stream> DownloadAsync(string filename)
    {
        return await storage.DownloadAsync(filename);
    }
}
```

### SAGA 模式

```csharp
// 定義 saga 步驟
public class CreateOrderStep : ISagaStep<OrderSagaData>
{
    public string Name => "CreateOrder";

    public async Task<ErrorOr<OrderSagaData>> ExecuteAsync(OrderSagaData data, CancellationToken ct)
    {
        // 建立訂單邏輯
        return data;
    }

    public async Task<ErrorOr<OrderSagaData>> CompensateAsync(OrderSagaData data, CancellationToken ct)
    {
        // 回滾訂單建立
        return data;
    }
}

// 執行 saga
var result = await sagaOrchestrator.ExecuteAsync(saga, initialData);
```

### 可觀測性設定

```json
{
  "Observability": {
    "ServiceName": "MyService",
    "Tracing": {
      "Enabled": true,
      "UseConsoleExporter": true,
      "OtlpEndpoint": "http://localhost:4317"
    },
    "Metrics": {
      "Enabled": true,
      "UseConsoleExporter": false
    }
  }
}
```

## 授權條款

本專案採用 MIT 授權條款。