---
title: 資料庫切換指南
description: 如何在 Weda Template 中切換不同的資料庫提供者
keywords: [Database, SQLite, PostgreSQL, MongoDB, InMemory, Entity Framework]
sidebar_position: 6
---

# 資料庫切換指南

> 學習如何在不同資料庫提供者之間切換

## 概觀

Weda Template 支援多種資料庫提供者，透過設定檔即可輕鬆切換，無需修改程式碼。

```
src/OpenClaw.Infrastructure/
├── Persistence/
│   ├── DatabaseOptions.cs      # 設定類別
│   └── DatabaseProvider.cs     # Provider 列舉
└── WedaTemplateInfrastructureModule.cs  # DI 註冊邏輯
```

---

## 1. 支援的資料庫

| Provider | 說明 | 適用場景 |
|----------|------|----------|
| `Sqlite` | 輕量級檔案資料庫 | 開發、測試、小型部署 |
| `PostgreSql` | 企業級關聯式資料庫 | 生產環境 |
| `MongoDb` | NoSQL 文件資料庫 | 需要彈性 Schema |
| `InMemory` | 記憶體資料庫 | 單元測試、快速原型 |

---

## 2. 設定方式

修改 `appsettings.json` 或 `appsettings.Development.json` 中的 `Database` 區段。

### 2.1 SQLite（預設）

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=OpenClaw.sqlite"
  }
}
```

**特點**：
- 無需額外安裝資料庫服務
- 資料儲存在單一檔案
- 適合快速開發與測試

### 2.2 PostgreSQL

```json
{
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=WedaTemplate;Username=postgres;Password=your_password"
  }
}
```

**特點**：
- 企業級效能與可靠性
- 支援進階 SQL 功能
- 適合生產環境

### 2.3 MongoDB

```json
{
  "Database": {
    "Provider": "MongoDb",
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "WedaTemplate"
  }
}
```

**特點**：
- 彈性的 Schema 設計
- 適合非結構化資料
- 水平擴展能力強

### 2.4 InMemory

```json
{
  "Database": {
    "Provider": "InMemory",
    "DatabaseName": "WedaTemplate"
  }
}
```

**特點**：
- 最快的存取速度
- 應用程式重啟後資料會遺失
- 適合單元測試

---

## 3. 實作原理

### 3.1 DatabaseOptions

```csharp
public class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
    public string ConnectionString { get; set; } = "Data Source=OpenClaw.sqlite";
    public string DatabaseName { get; set; } = "WedaTemplate";
}
```

### 3.2 DatabaseProvider Enum

```csharp
public enum DatabaseProvider
{
    Sqlite,
    PostgreSql,
    MongoDb,
    InMemory
}
```

### 3.3 DI 註冊邏輯

```csharp
private static IServiceCollection AddDatabase(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var options = configuration
        .GetSection(DatabaseOptions.SectionName)
        .Get<DatabaseOptions>() ?? new DatabaseOptions();

    services.AddDbContext<AppDbContext>(dbOptions =>
    {
        _ = options.Provider switch
        {
            DatabaseProvider.Sqlite =>
                dbOptions.UseSqlite(options.ConnectionString),
            DatabaseProvider.PostgreSql =>
                dbOptions.UseNpgsql(options.ConnectionString),
            DatabaseProvider.MongoDb =>
                dbOptions.UseMongoDB(
                    options.ConnectionString,
                    options.DatabaseName),
            DatabaseProvider.InMemory =>
                dbOptions.UseInMemoryDatabase(options.DatabaseName),
            _ => throw new InvalidOperationException(
                $"Unsupported provider: {options.Provider}")
        };
    });

    return services;
}
```

---

## 4. 測試步驟

### 4.1 測試 SQLite（預設）

```bash
cd src/OpenClaw.Api
dotnet run
```

開啟 http://localhost:5001 使用 Swagger UI 測試 CRUD。

### 4.2 測試 PostgreSQL

**前置條件**：使用 Docker 啟動 PostgreSQL

```bash
docker run --name weda-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=WedaTemplate \
  -p 5432:5432 \
  -d postgres:16
```

修改 `appsettings.Development.json` 後執行：

```bash
dotnet run
```

### 4.3 測試 MongoDB

**前置條件**：使用 Docker 啟動 MongoDB

```bash
docker run --name weda-mongo \
  -p 27017:27017 \
  -d mongo:7
```

修改 `appsettings.Development.json` 後執行：

```bash
dotnet run
```

### 4.4 測試 InMemory

修改 `appsettings.Development.json` 後執行：

```bash
dotnet run
```

> **注意**：InMemory 資料庫在應用程式重啟後資料會遺失。

---

## 5. 驗證測試

對每個 Provider 執行以下操作來驗證功能：

### 5.1 建立員工

```bash
curl -X POST http://localhost:5001/api/v1/employees \
  -H "Content-Type: application/json" \
  -d '{
    "name": "John Doe",
    "email": "john@example.com",
    "department": "Engineering",
    "position": "Developer",
    "hireDate": "2024-01-15"
  }'
```

**預期回應**：`201 Created` 並回傳員工資料。

### 5.2 查詢所有員工

```bash
curl http://localhost:5001/api/v1/employees
```

**預期回應**：包含剛建立的員工。

### 5.3 重啟後驗證（除 InMemory 外）

```bash
# 停止伺服器 (Ctrl+C)
dotnet run

# 再次查詢
curl http://localhost:5001/api/v1/employees
```

**預期回應**：資料仍然存在（SQLite、PostgreSQL、MongoDB）。

---

## 6. 清理測試環境

```bash
# 停止並移除 Docker Container
docker stop weda-postgres weda-mongo
docker rm weda-postgres weda-mongo

# 刪除 SQLite 檔案
rm src/OpenClaw.Api/OpenClaw.sqlite
```

---

## 7. 注意事項

### 7.1 MongoDB 限制

EF Core 對 MongoDB 的支援較新，某些進階查詢可能不支援。建議在使用前確認所需功能是否支援。

### 7.2 Migration 策略

目前使用 `EnsureDatabaseCreated` 自動建立資料表。正式環境建議改用 EF Core Migrations：

```bash
# 建立 Migration
dotnet ef migrations add InitialCreate

# 套用 Migration
dotnet ef database update
```

### 7.3 連線字串安全

生產環境請使用以下方式管理敏感資訊：

- **User Secrets**（開發環境）
  ```bash
  dotnet user-secrets set "Database:ConnectionString" "your-connection-string"
  ```

- **環境變數**（生產環境）
  ```bash
  export Database__ConnectionString="your-connection-string"
  ```

---

## 快速參考

### Provider 比較

| 特性 | SQLite | PostgreSQL | MongoDB | InMemory |
|------|--------|------------|---------|----------|
| 持久化 | ✅ | ✅ | ✅ | ❌ |
| 交易支援 | ✅ | ✅ | 部分 | ✅ |
| 水平擴展 | ❌ | 有限 | ✅ | ❌ |
| 設定複雜度 | 低 | 中 | 中 | 最低 |
| 適用場景 | 開發/小型 | 生產 | 彈性 Schema | 測試 |

### 常見問題

**Q: 切換 Provider 後需要修改 Repository 嗎？**

A: 不需要。Repository 透過 EF Core 的抽象層運作，Provider 的切換對上層透明。

**Q: 如何在測試中使用 InMemory 資料庫？**

A: 在測試專案中覆寫設定即可：

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TestDb"));
```

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer 指南
- [Entity Framework Core - Database Providers](https://learn.microsoft.com/ef/core/providers/)
