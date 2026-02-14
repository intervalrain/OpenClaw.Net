# Database Provider Switch Guide

本文件說明如何在 Weda Template 中切換不同的資料庫提供者。

## 支援的資料庫

| Provider | 說明 | 適用場景 |
|----------|------|----------|
| `Sqlite` | 輕量級檔案資料庫 | 開發、測試、小型部署 |
| `PostgreSql` | 企業級關聯式資料庫 | 生產環境 |
| `MongoDb` | NoSQL 文件資料庫 | 需要彈性 schema |
| `InMemory` | 記憶體資料庫 | 單元測試、快速原型 |

## 設定方式

修改 `appsettings.json` 或 `appsettings.Development.json` 中的 `Database` 區段：

### SQLite (預設)

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=OpenClaw.sqlite"
  }
}
```

### PostgreSQL

```json
{
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=WedaTemplate;Username=postgres;Password=your_password"
  }
}
```

### MongoDB

```json
{
  "Database": {
    "Provider": "MongoDb",
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "WedaTemplate"
  }
}
```

### InMemory

```json
{
  "Database": {
    "Provider": "InMemory",
    "DatabaseName": "WedaTemplate"
  }
}
```

## 測試步驟

### 1. 測試 SQLite (預設)

```bash
# 確認設定為 Sqlite
cd src/OpenClaw.Api
dotnet run
```

開啟 http://localhost:5001 使用 Swagger UI 測試 CRUD。

### 2. 測試 PostgreSQL

**前置條件**: 安裝並啟動 PostgreSQL

```bash
# 使用 Docker 快速啟動 PostgreSQL
docker run --name weda-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=WedaTemplate \
  -p 5432:5432 \
  -d postgres:16
```

修改 `appsettings.Development.json`：

```json
{
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=WedaTemplate;Username=postgres;Password=postgres"
  }
}
```

```bash
dotnet run
```

### 3. 測試 MongoDB

**前置條件**: 安裝並啟動 MongoDB

```bash
# 使用 Docker 快速啟動 MongoDB
docker run --name weda-mongo \
  -p 27017:27017 \
  -d mongo:7
```

修改 `appsettings.Development.json`：

```json
{
  "Database": {
    "Provider": "MongoDb",
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "WedaTemplate"
  }
}
```

```bash
dotnet run
```

### 4. 測試 InMemory

修改 `appsettings.Development.json`：

```json
{
  "Database": {
    "Provider": "InMemory",
    "DatabaseName": "WedaTemplate"
  }
}
```

```bash
dotnet run
```

> **注意**: InMemory 資料庫在應用程式重啟後資料會遺失。

## 驗證測試

對每個 provider 執行以下操作：

### Create Employee

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

預期回應：`201 Created` 並回傳員工資料。

### Get All Employees

```bash
curl http://localhost:5001/api/v1/employees
```

預期回應：包含剛建立的員工。

### 重啟後驗證 (除 InMemory 外)

```bash
# 停止伺服器 (Ctrl+C)
dotnet run

# 再次查詢
curl http://localhost:5001/api/v1/employees
```

預期回應：資料仍然存在（SQLite、PostgreSQL、MongoDB）。

## 清理測試環境

```bash
# 停止並移除 Docker containers
docker stop weda-postgres weda-mongo
docker rm weda-postgres weda-mongo

# 刪除 SQLite 檔案
rm src/OpenClaw.Api/OpenClaw.sqlite
```

## 注意事項

1. **MongoDB 限制**: EF Core 對 MongoDB 的支援較新，某些進階查詢可能不支援。
2. **Migration**: 目前使用 `EnsureDatabaseCreated`，正式環境建議改用 EF Core Migrations。
3. **連線字串安全**: 生產環境請使用 User Secrets 或環境變數，不要將密碼寫在設定檔。

## 相關檔案

- [DatabaseOptions.cs](../src/OpenClaw.Infrastructure/Persistence/DatabaseOptions.cs) - 設定類別
- [DatabaseProvider.cs](../src/OpenClaw.Infrastructure/Persistence/DatabaseProvider.cs) - Provider enum
- [WedaTemplateInfrastructureModule.cs](../src/OpenClaw.Infrastructure/WedaTemplateInfrastructureModule.cs) - DI 註冊邏輯
