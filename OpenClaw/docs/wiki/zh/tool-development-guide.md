---
sidebar_position: 4
sidebar_label: '工具開發'
hide_title: true
title: '工具開發指南'
keywords: ['OpenClaw', '工具', '外掛', '開發', 'C#']
description: '學習如何為 OpenClaw.NET 建立自訂 C# 工具'
---

# 工具開發指南

> 建立自訂 C# 工具以擴展 Agent 能力

## 概述

工具是強型別的 C# 類別，為 AI Agent 提供可執行的能力。每個工具代表一個特定動作（例如：讀取檔案、發送 HTTP 請求、執行 Shell 指令）。工具透過組件掃描自動註冊 — 只需放在 `src/tools/` 下即可供 Agent 使用。

## 架構

```
┌─────────────────────────────────────────────────────────────┐
│                    您的工具專案                               │
│              OpenClaw.Tools.MyTools                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│    ┌─────────────────────────────────────────────────┐      │
│    │     YourTool : AgentToolBase<YourToolArgs>       │      │
│    └─────────────────────────────────────────────────┘      │
│                            │                                │
│                            ▼                                │
│    ┌─────────────────────────────────────────────────┐      │
│    │          OpenClaw.Contracts (NuGet)              │      │
│    │  - IAgentTool                                    │      │
│    │  - AgentToolBase<TArgs>                          │      │
│    │  - SkillResult, SkillContext                     │      │
│    └─────────────────────────────────────────────────┘      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

工具僅依賴 `OpenClaw.Contracts`，保持輕量且易於發布。

## 快速開始

### 1. 建立工具專案

```bash
cd OpenClaw/src/tools
dotnet new classlib -n OpenClaw.Tools.MyTools
cd OpenClaw.Tools.MyTools
dotnet add reference ../../OpenClaw.Contracts/OpenClaw.Contracts.csproj
```

### 2. 定義您的工具

```csharp
using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.MyTools;

public class GreetTool(IServiceProvider sp) : AgentToolBase<GreetToolArgs>
{
    public override string Name => "greet";
    public override string Description => "Greet a person by name.";

    public override async Task<SkillResult> ExecuteAsync(
        GreetToolArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Name))
            return SkillResult.Failure("Name is required.");

        return SkillResult.Success($"Hello, {args.Name}!");
    }
}

public record GreetToolArgs(
    [property: Description("The name of the person to greet")]
    string? Name
);
```

### 3. 在方案中註冊

在 `OpenClaw.Api.csproj` 中加入專案參考：

```xml
<ProjectReference Include="..\tools\OpenClaw.Tools.MyTools\OpenClaw.Tools.MyTools.csproj" />
```

工具會透過組件掃描自動探索並註冊，無需手動註冊。

## 核心概念

### AgentToolBase<TArgs>

泛型基底類別，負責處理：
- 從 `TArgs` record 屬性**產生 JSON Schema**
- 從 LLM 工具呼叫回應**反序列化引數**
- **錯誤處理**與結果包裝
- 透過建構式 `IServiceProvider` 進行**依賴注入**

### TArgs Record

將工具參數定義為 C# `record`：

```csharp
public record MyToolArgs(
    [property: Description("Required parameter")]
    string RequiredParam,

    [property: Description("Optional parameter")]
    string? OptionalParam,

    [property: Description("Numeric parameter")]
    int? Count
);
```

**型別對應至 JSON Schema：**

| C# 型別 | JSON Schema 型別 |
|---------|------------------|
| `string` | `string` |
| `int`, `long`, `short` | `integer` |
| `float`, `double`, `decimal` | `number` |
| `bool` | `boolean` |
| `T[]`, `List<T>` | `array` |

**Nullable 型別**（`string?`、`int?`）視為選填參數。

### SkillResult

回傳執行結果給 LLM：

```csharp
// 成功並帶有文字輸出
return SkillResult.Success("Operation completed.");

// 成功並帶有結構化資料
return SkillResult.Success(JsonSerializer.Serialize(data));

// 失敗並帶有錯誤訊息
return SkillResult.Failure("File not found.");
```

### 存取服務

透過建構式注入的 `IServiceProvider` 解析服務：

```csharp
public class MyTool(IServiceProvider sp) : AgentToolBase<MyToolArgs>
{
    public override async Task<SkillResult> ExecuteAsync(
        MyToolArgs args, CancellationToken ct)
    {
        var dbContext = sp.GetRequiredService<AppDbContext>();
        var httpClient = sp.GetRequiredService<IHttpClientFactory>();
        // ...
    }
}
```

## 最佳實踐

### 1. 使用描述性名稱

```csharp
// snake_case，動詞 + 名詞
public override string Name => "read_file";
public override string Description =>
    "Read the contents of a file at the specified path.";
```

### 2. 提早驗證輸入

```csharp
public override async Task<SkillResult> ExecuteAsync(
    MyToolArgs args, CancellationToken ct)
{
    if (string.IsNullOrEmpty(args.Path))
        return SkillResult.Failure("Path is required.");

    if (!File.Exists(args.Path))
        return SkillResult.Failure($"File not found: {args.Path}");

    // 繼續...
}
```

### 3. 處理取消請求

```csharp
public override async Task<SkillResult> ExecuteAsync(
    MyToolArgs args, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    var content = await File.ReadAllTextAsync(args.Path, ct);
    return SkillResult.Success(content);
}
```

### 4. 遵守路徑安全

對於存取檔案系統的工具，使用路徑安全工具：

```csharp
var pathSecurity = sp.GetRequiredService<IPathSecurity>();
if (!pathSecurity.IsPathAllowed(args.Path))
    return SkillResult.Failure("Access denied: path is outside workspace.");
```

## 內建工具參考

| 工具 | 名稱 | 說明 |
|------|------|------|
| FileSystem | `read_file`, `write_file`, `list_directory` | 檔案操作，具備路徑遍歷防護 |
| Git | `git` | Git 操作（diff、log、status、clone） |
| GitHub | `github` | 透過 GitHub CLI 管理 PR/Issue |
| AzureDevOps | `azure_devops` | 工作項目、儲存庫、建置、PR |
| Shell | `execute_command` | 受限的命令執行 |
| Http | `http_request` | HTTP 請求（GET/POST/PUT） |
| WebSearch | `web_search` | SearXNG 網頁搜尋 |
| Pdf | `read_pdf`, `search_pdf` | PDF 讀取與搜尋 |
| ImageGen | `generate_image` | DALL-E 圖片生成 |
| Notion | `notion` | 頁面/資料庫操作 |
| Tmux | `tmux` | 終端機多工控制 |
| Preference | `preference` | 使用者偏好管理 |

## 相關資源

- [Markdown 技能指南](./markdown-skills-guide.md) — 組合工具的宣告式技能
- [架構指南](./architecture.md) — 系統設計概觀
