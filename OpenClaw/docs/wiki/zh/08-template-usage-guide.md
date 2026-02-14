---
title: dotnet new Template 使用指南
description: 如何安裝、使用與自訂 Weda Template
keywords: [dotnet new, Template, Installation, Configuration]
sidebar_position: 9
---

# dotnet new Template 使用指南

> 學習如何安裝與使用 Weda Clean Architecture Template

## 概觀

Weda Template 是一個 `dotnet new` 專案範本，可透過命令列快速建立符合 Clean Architecture 的專案結構。

```
.template.config/
└── template.json    # Template 設定檔
```

---

## 1. 安裝 Template

### 1.1 從本地安裝

```bash
# 進入 Template 根目錄
cd WedaTemplate

# 安裝 Template
dotnet new install .

# 確認安裝成功
dotnet new list | grep weda
```

### 1.2 解除安裝

```bash
dotnet new uninstall WedaTemplate
```

---

## 2. 建立新專案

### 2.1 基本用法

```bash
# 建立新專案（使用預設設定）
dotnet new weda -n MyProject

# 指定輸出目錄
dotnet new weda -n MyProject -o ./src/MyProject
```

### 2.2 查看可用參數

```bash
dotnet new weda --help
```

---

## 3. Template 參數

### 3.1 資料庫選項 (`--db`)

選擇要使用的資料庫提供者：

| 值 | 說明 | 預設 |
|----|------|------|
| `sqlite` | SQLite 輕量級資料庫 | ✅ |
| `postgres` | PostgreSQL |  |
| `mongo` | MongoDB |  |
| `none` | InMemory（無外部資料庫） |  |

```bash
# 使用 PostgreSQL
dotnet new weda -n MyProject --db postgres

# 使用 MongoDB
dotnet new weda -n MyProject --db mongo

# 使用 InMemory
dotnet new weda -n MyProject --db none
```

### 3.2 NATS 服務名稱 (`--Nats`)

設定 NATS 服務的名稱：

```bash
dotnet new weda -n MyProject --Nats my-service
```

預設值：`openclaw`

### 3.3 測試專案 (`--test`)

是否包含測試專案：

```bash
# 不包含測試專案
dotnet new weda -n MyProject --test false
```

預設值：`true`

### 3.4 Wiki 文件 (`--wiki`)

是否包含 Wiki 文件與產生器：

```bash
# 不包含 Wiki
dotnet new weda -n MyProject --wiki false
```

預設值：`true`

### 3.5 範例模組 (`--sample`)

是否包含 Employee 範例模組：

```bash
# 不包含範例模組
dotnet new weda -n MyProject --sample false
```

預設值：`true`

---

## 4. 常見組合

### 4.1 最小化專案（生產環境）

```bash
dotnet new weda -n MyProject \
  --db postgres \
  --sample false \
  --wiki false
```

### 4.2 開發學習用

```bash
dotnet new weda -n MyProject \
  --db sqlite \
  --sample true \
  --wiki true \
  --test true
```

### 4.3 快速原型

```bash
dotnet new weda -n MyProject \
  --db none \
  --sample false \
  --test false
```

---

## 5. Template 設定檔說明

### 5.1 template.json 結構

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Rain Hu",
  "classifications": ["Web", "API", "Clean Architecture", "DDD", "CQRS"],
  "identity": "OpenClaw",
  "name": "Weda Clean Architecture Template",
  "shortName": "weda",
  "description": "A clean architecture template with DDD, CQRS, NATS EventController, and Entity Framework Core.",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "OpenClaw",
  "preferNameDirectory": true
}
```

### 5.2 參數定義

```json
{
  "symbols": {
    "db": {
      "type": "parameter",
      "description": "Database provider to use.",
      "datatype": "choice",
      "choices": [
        { "choice": "sqlite", "description": "SQLite (default)" },
        { "choice": "postgres", "description": "PostgreSQL" },
        { "choice": "mongo", "description": "MongoDB" },
        { "choice": "none", "description": "InMemory" }
      ],
      "defaultValue": "sqlite"
    }
  }
}
```

### 5.3 條件式檔案排除

```json
{
  "sources": [
    {
      "modifiers": [
        {
          "condition": "(!test)",
          "exclude": ["tests/**/*"]
        },
        {
          "condition": "(!sample)",
          "exclude": [
            "src/OpenClaw.Api/Employees/**/*",
            "src/OpenClaw.Domain/Employees/**/*",
            "src/OpenClaw.Application/Employees/**/*",
            "src/OpenClaw.Contracts/Employees/**/*",
            "src/OpenClaw.Infrastructure/Employees/**/*"
          ]
        }
      ]
    }
  ]
}
```

### 5.4 條件式編譯符號

Template 使用 `#if` 預處理指令：

```csharp
services.AddDbContext<AppDbContext>(dbOptions =>
{
#if sqlite
    dbOptions.UseSqlite(options.ConnectionString);
#elif postgres
    dbOptions.UseNpgsql(options.ConnectionString);
#elif mongo
    dbOptions.UseMongoDB(options.ConnectionString, options.DatabaseName);
#elif nodb
    dbOptions.UseInMemoryDatabase(options.DatabaseName);
#endif
});
```

---

## 6. 建立後步驟

### 6.1 還原套件

```bash
cd MyProject
dotnet restore
```

### 6.2 建置專案

```bash
dotnet build
```

### 6.3 執行應用程式

```bash
cd src/MyProject.Api
dotnet run
```

### 6.4 執行測試

```bash
dotnet test
```

---

## 7. 自訂 Template

### 7.1 新增參數

在 `template.json` 的 `symbols` 區段新增：

```json
{
  "symbols": {
    "useRedis": {
      "type": "parameter",
      "description": "Include Redis caching support.",
      "datatype": "bool",
      "defaultValue": "false"
    }
  }
}
```

### 7.2 新增條件式程式碼

```csharp
#if useRedis
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});
#endif
```

### 7.3 新增條件式檔案

```json
{
  "sources": [
    {
      "modifiers": [
        {
          "condition": "(!useRedis)",
          "exclude": ["src/**/Redis/**/*"]
        }
      ]
    }
  ]
}
```

---

## 快速參考

### 完整參數列表

| 參數 | 類型 | 預設值 | 說明 |
|------|------|--------|------|
| `--db` | choice | `sqlite` | 資料庫提供者 |
| `--Nats` | string | `openclaw` | NATS 服務名稱 |
| `--test` | bool | `true` | 包含測試專案 |
| `--wiki` | bool | `true` | 包含 Wiki 文件 |
| `--sample` | bool | `true` | 包含範例模組 |

### 產生的專案結構

```
MyProject/
├── src/
│   ├── MyProject.Api/           # API Layer
│   ├── MyProject.Application/   # Application Layer
│   ├── MyProject.Contracts/     # DTOs & Requests
│   ├── MyProject.Domain/        # Domain Layer
│   ├── MyProject.Infrastructure/# Infrastructure Layer
│   └── Weda.Core/               # 共用核心庫
├── tests/                       # (--test true)
│   ├── MyProject.Domain.UnitTests/
│   ├── MyProject.Application.UnitTests/
│   ├── MyProject.Infrastructure.UnitTests/
│   ├── MyProject.Api.IntegrationTests/
│   └── MyProject.TestCommon/
├── docs/                        # (--wiki true)
│   └── wiki/
└── tools/                       # (--wiki true)
    └── WikiGenerator/
```

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [05-database-switch-guide.md](05-database-switch-guide.md) - 資料庫切換指南
- [dotnet new Templates Documentation](https://learn.microsoft.com/dotnet/core/tools/custom-templates)
