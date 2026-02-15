---
sidebar_position: 2
sidebar_label: 'Agent Skill 指南'
hide_title: true
title: 'Agent Skill 開發指南'
keywords: ['OpenClaw', 'Skill', 'Plugin', 'Development', 'Tutorial']
description: '學習如何為 OpenClaw.NET 建立自訂 Skill'
---

# Agent Skill 開發指南

> 學習如何為 OpenClaw.NET 建立自訂 Skill

## 概述

Skill 是讓 AI Agent 與真實世界互動的基礎模組。每個 Skill 代表一項特定能力（例如：讀取檔案、發送 HTTP 請求、執行 Shell 指令）。

## 架構

```
┌─────────────────────────────────────────────────────────────────┐
│                       Your Skill NuGet                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│    ┌─────────────────────────────────────────────────────┐      │
│    │      YourSkill : AgentSkillBase<YourSkillArgs>      │      │
│    └─────────────────────────────────────────────────────┘      │
│                              │                                  │
│                              ▼                                  │
│    ┌─────────────────────────────────────────────────────┐      │
│    │            OpenClaw.Contracts (NuGet)               │      │
│    │  - IAgentSkill                                      │      │
│    │  - AgentSkillBase<TArgs>                            │      │
│    │  - SkillResult, SkillContext                        │      │
│    └─────────────────────────────────────────────────────┘      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

Skill 僅依賴 `OpenClaw.Contracts`，使其輕量且易於發布。

## 快速開始

### 1. 建立新的 Class Library

```bash
dotnet new classlib -n OpenClaw.Skills.MySkills
cd OpenClaw.Skills.MySkills
dotnet add package OpenClaw.Contracts
```

### 2. 定義你的 Skill

```csharp
using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.MySkills;

public class GreetSkill : AgentSkillBase<GreetSkillArgs>
{
    public override string Name => "greet";
    public override string Description => "Greet a person by name.";

    protected override Task<SkillResult> ExecuteAsync(GreetSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Name))
        {
            return Task.FromResult(SkillResult.Failure("Name is required."));
        }

        var greeting = $"Hello, {args.Name}!";
        return Task.FromResult(SkillResult.Success(greeting));
    }
}

public record GreetSkillArgs(
    [property: Description("The name of the person to greet")]
    string? Name
);
```

### 3. 註冊至 Agent Pipeline

```csharp
var skills = new IAgentSkill[]
{
    new GreetSkill(),
    // ... 其他 skills
};

var pipeline = new AgentPipeline(llmProvider, skills, options);
```

## 核心概念

### AgentSkillBase<TArgs>

泛型基底類別，負責處理：
- 從 `TArgs` 屬性產生 JSON Schema
- 引數反序列化
- 錯誤處理

### TArgs Record

將 Skill 的參數定義為 `record`：

```csharp
public record MySkillArgs(
    [property: Description("Required parameter")]
    string RequiredParam,

    [property: Description("Optional parameter")]
    string? OptionalParam,

    [property: Description("Numeric parameter")]
    int? Count
);
```

**型別對應：**

| C# Type | JSON Schema Type |
|---------|------------------|
| `string` | `string` |
| `int`, `long`, `short` | `integer` |
| `float`, `double`, `decimal` | `number` |
| `bool` | `boolean` |
| `T[]`, `List<T>` | `array` |

**Nullable 型別**（`string?`、`int?`）視為選填參數。

### SkillResult

回傳執行結果：

```csharp
// 成功並帶有輸出
return SkillResult.Success("Operation completed successfully.");

// 成功並帶有結構化資料
return SkillResult.Success(JsonSerializer.Serialize(data));

// 失敗並帶有錯誤訊息
return SkillResult.Failure("File not found.");
```

### SkillContext

包含執行情境（目前包含 JSON 字串格式的 Arguments）。

## 最佳實踐

### 1. 提早驗證輸入

```csharp
protected override Task<SkillResult> ExecuteAsync(MyArgs args, CancellationToken ct)
{
    if (string.IsNullOrEmpty(args.Path))
    {
        return Task.FromResult(SkillResult.Failure("Path is required."));
    }

    // 繼續執行...
}
```

### 2. 使用描述性名稱

```csharp
public override string Name => "read_file";  // snake_case，動詞 + 名詞
public override string Description => "Read the contents of a file at the specified path.";
```

### 3. 處理取消請求

```csharp
protected override async Task<SkillResult> ExecuteAsync(MyArgs args, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    var content = await File.ReadAllTextAsync(args.Path, ct);
    return SkillResult.Success(content);
}
```

### 4. 提供清楚的錯誤訊息

```csharp
if (!File.Exists(args.Path))
{
    return SkillResult.Failure($"File not found: {args.Path}");
}
```

## 範例：FileSystem Skills

參考 [OpenClaw.Skills.FileSystem](../skills/OpenClaw.Skills.FileSystem/) 的實作範例：

- **ReadFileSkill** - 讀取檔案內容
- **WriteFileSkill** - 將內容寫入檔案
- **ListDirectorySkill** - 列出目錄內容

## 發布

將你的 Skill 打包為 NuGet：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>OpenClaw.Skills.MySkills</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OpenClaw.Contracts" Version="1.0.0" />
  </ItemGroup>
</Project>
```

使用者透過以下方式安裝：

```bash
dotnet add package OpenClaw.Skills.MySkills
```

---

## 相關資源

- [簡介](./introduction.md)

import Revision from '@site/src/components/Revision';

<Revision date="Feb-15, 2026" version="v1.0.0" />
