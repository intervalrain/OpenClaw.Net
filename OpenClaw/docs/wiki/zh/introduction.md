---
sidebar_position: 1
sidebar_label: '簡介'
hide_title: true
title: 'OpenClaw.NET - AI Agent 執行環境'
keywords: ['OpenClaw', 'AI Agent', '.NET', 'LLM', 'Plugin Architecture']
description: '以 C#/.NET 建構的強型別、外掛導向 AI Agent 執行環境'
---

# OpenClaw.NET

> 以 C#/.NET 建構的強型別、外掛導向 AI Agent 執行環境

## 概述

OpenClaw.NET 是一個企業級 AI Agent 平台，透過可擴充的 Skill 系統，讓 LLM 驅動的 Agent 能夠執行真實世界的任務。本專案著重於工程品質、型別安全與長期可維護性。

## 主要特色

- **強型別** - 所有 Skill 皆於編譯時期檢查
- **外掛架構** - 透過 NuGet 套件擴充功能
- **LLM 不可知** - 支援多種 LLM 供應商（Ollama、OpenAI 等）
- **Pipeline 導向** - 採用 Middleware 模式處理請求

## 架構

```
┌─────────────────────────────────────────────────────────────────┐
│                      OpenClaw.NET Architecture                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│    [ Channel Adapter ]      CLI, WebSocket, Telegram...         │
│            │                                                    │
│            ▼                                                    │
│    [ Agent Pipeline ]       Middleware chain                    │
│            │                                                    │
│            ▼                                                    │
│    [ LLM Provider ]         Ollama, OpenAI, Azure...            │
│            │                                                    │
│            ▼                                                    │
│    [ Skill Executor ]       FileSystem, Shell, HTTP...          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## 專案結構

```
OpenClaw/
├── src/
│   ├── OpenClaw.Contracts/                  # 介面與 DTO（Skill 的 SPI）
│   ├── OpenClaw.Application/                # Agent Pipeline、Use Cases
│   ├── OpenClaw.Domain/                     # Domain Models
│   ├── OpenClaw.Infrastructure/             # 資料庫、外部服務
│   ├── OpenClaw.Infrastructure.Llm.Ollama/  # Ollama Provider
│   ├── OpenClaw.Api/                        # REST API
│   └── OpenClaw.Cli/                        # CLI 應用程式
├── skills/
│   └── OpenClaw.Skills.FileSystem/          # 檔案操作（讀取、寫入、列表）
├── tests/
└── docs/
```

## 快速開始

### 環境需求

- Docker & Docker Compose
- .NET 10 SDK（本機開發用）
- 已安裝模型的 Ollama（例如 `ollama pull qwen2.5:7b`）

### 使用 Docker Compose 執行（推薦）

啟動所有服務（API + PostgreSQL）：

```bash
docker compose up -d
```

查看 logs：

```bash
docker compose logs -f
```

停止服務：

```bash
docker compose down
```

### 存取端點

| 服務 | URL |
|------|-----|
| API | http://localhost:5001 |
| Swagger UI | http://localhost:5001/swagger |
| Wiki | http://localhost:5001/wiki |

### 本機開發

僅啟動 PostgreSQL 容器：

```bash
docker compose up -d postgres
```

本機執行 API：

```bash
dotnet run --project src/OpenClaw.Api
```

### 執行 CLI

```bash
cd OpenClaw
dotnet run --project src/OpenClaw.Cli
```

### 互動範例

```
> list files in current directory

The files in the current directory are:
- README.md
- src/
- docs/
...
```

## 建立自訂 Skill

```csharp
public class MySkill : AgentSkillBase<MySkillArgs>
{
    public override string Name => "my_skill";
    public override string Description => "Does something useful";

    protected override Task<SkillResult> ExecuteAsync(MySkillArgs args, CancellationToken ct)
    {
        // 實作
        return Task.FromResult(SkillResult.Success("Done!"));
    }
}

public record MySkillArgs(
    [property: Description("Parameter description")]
    string? Param1
);
```

詳見 [Agent Skill 開發指南](./agent-skill-guide.md)。

---

## 相關資源

- [Agent Skill 開發指南](./agent-skill-guide.md)

import Revision from '@site/src/components/Revision';

<Revision date="Feb-15, 2026" version="v1.0.0" />