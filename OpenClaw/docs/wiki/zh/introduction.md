---
sidebar_position: 1
sidebar_label: '簡介'
hide_title: true
title: 'OpenClaw.NET - AI Agent 平台'
keywords: ['OpenClaw', 'AI Agent', '.NET', 'LLM', '企業級平台']
description: '以 .NET 建構的企業級 AI Agent 平台，具備多使用者隔離、模組化工具與排程自動化'
---

# OpenClaw.NET

> 以 .NET 建構的企業級 AI Agent 平台

## 概述

OpenClaw.NET 是一個自託管的 AI Agent 平台，透過模組化工具系統讓 LLM 驅動的 Agent 能夠執行真實世界的任務。採用 Clean Architecture 與領域驅動設計（DDD），提供多使用者工作區隔離、雙層模型供應商管理、排程自動化，以及完善的安全基礎架構。

## 主要特色

### AI Agent 能力
- **多供應商 LLM 支援** — Ollama、OpenAI、Anthropic，以及任何 OpenAI 相容端點
- **雙層模型供應商** — SuperAdmin 管理全域供應商；使用者可用自己的 API Key 新增個人供應商
- **模組化工具系統** — 12 個內建 C# 工具，透過組件掃描自動註冊
- **Markdown 技能** — 宣告式技能定義（`SKILL.md`），將工具與 LLM 指令組合
- **即時串流** — SSE（Server-Sent Events）串流聊天回應
- **上下文壓縮** — 長對話自動壓縮對話歷史
- **視覺支援** — 透過 OpenAI Vision API 上傳與分析圖片

### 多使用者與工作區
- **使用者級資料隔離** — 透過 EF Core 全域查詢篩選器，所有資料依認證使用者劃分
- **工作區管理** — 個人與共享工作區，具備角色存取控制（檢視者/成員/擁有者）
- **檔案系統隔離** — 每位使用者限定在自己的工作區目錄，防止路徑遍歷攻擊
- **目錄權限** — 每個目錄可設定可見性（預設/公開/公開唯讀/私有）

### 自動化
- **CronJob 排程器** — 基於 Cron 表達式的排程，搭配 LLM 輔助執行
- **工具實例** — 預設參數的工具，可在 CronJob 中重複使用
- **Agent 活動追蹤** — 僅附加的活動日誌，用於監控與視覺化

### 安全性
- **JWT 認證** — 基於 Token 的認證，具備 Refresh Token 輪替與帳號鎖定
- **角色存取控制** — 三層角色（User / Admin / SuperAdmin）搭配細粒度權限
- **加密儲存** — AES-256 加密 API Key 與機密資料
- **內容安全政策** — 嚴格 CSP 執行，禁止內嵌腳本
- **稽核日誌** — 所有安全關鍵操作均記錄（僅 SuperAdmin 可檢視）
- **速率限制** — 登入與註冊速率限制

### 整合
- **Telegram Bot** — 頻道轉接器，支援每使用者設定
- **Azure DevOps** — 工作項目、儲存庫、建置、管線與 PR
- **GitHub** — Issue、PR 與 CI 工作流程
- **Notion** — 頁面、資料庫、搜尋與留言
- **Web 搜尋** — 整合 SearXNG 搜尋引擎
- **更多** — Shell、HTTP、PDF、圖片生成、Tmux

## 架構

```
                    +------------------+
                    |     頻道層       |
                    |  Web UI / Telegram|
                    +--------+---------+
                             |
                    +--------v---------+
                    |      API 層      |
                    |   ASP.NET Core   |
                    |  (Controllers)   |
                    +--------+---------+
                             |
                    +--------v---------+
                    |    應用程式層     |
                    |  CQRS + Mediator |
                    |  Agent Pipeline  |
                    +--------+---------+
                             |
              +--------------+--------------+
              |              |              |
     +--------v---+  +------v------+  +----v--------+
     |   領域層   |  |  基礎設施層  |  |   工具層    |
     |  實體/聚合 |  | EF Core, JWT |  |  12 個模組  |
     |           |  | NATS, Email  |  | FileSystem  |
     +------------+  +-------------+  | Git, Shell..|
                                      +-------------+
```

## 專案結構

```
OpenClaw/
├── src/
│   ├── OpenClaw.Api/                    # REST API + 靜態前端
│   ├── OpenClaw.Application/            # 業務邏輯、CQRS、Agent Pipeline
│   ├── OpenClaw.Contracts/              # 介面、DTO、共用型別
│   ├── OpenClaw.Domain/                 # 領域實體、聚合根、Repository
│   ├── OpenClaw.Infrastructure/         # EF Core、安全、持久化、服務
│   ├── OpenClaw.Channels.Telegram/      # Telegram Bot 頻道轉接器
│   ├── OpenClaw.Hosting/                # DI 註冊、可觀測性
│   ├── Weda.Core/                       # 共用框架（CQRS、NATS、安全）
│   └── tools/                           # 12 個內建工具模組
│       ├── OpenClaw.Tools.FileSystem/
│       ├── OpenClaw.Tools.Git/
│       ├── OpenClaw.Tools.GitHub/
│       ├── OpenClaw.Tools.AzureDevOps/
│       ├── OpenClaw.Tools.Shell/
│       ├── OpenClaw.Tools.Http/
│       ├── OpenClaw.Tools.WebSearch/
│       ├── OpenClaw.Tools.Pdf/
│       ├── OpenClaw.Tools.ImageGen/
│       ├── OpenClaw.Tools.Notion/
│       ├── OpenClaw.Tools.Tmux/
│       └── OpenClaw.Tools.Preference/   # 使用者偏好管理
├── skills/                              # Markdown 技能定義（SKILL.md）
├── tests/                               # 單元、整合、遊樂場測試
├── docs/                                # 文件與 Wiki
└── docker-compose.yml                   # 完整服務編排
```

## 技術堆疊

| 層級 | 技術 |
|------|------|
| 執行環境 | .NET 10 |
| Web 框架 | ASP.NET Core + Mediator (CQRS) |
| 資料庫 | PostgreSQL + Entity Framework Core |
| 訊息佇列 | NATS JetStream |
| 安全 | JWT、AES-256、CSP、稽核日誌 |
| 前端 | 原生 JavaScript（CSP 合規） |
| 搜尋 | SearXNG |
| 可觀測性 | OpenTelemetry |
| 容器化 | Docker + Docker Compose |

## 相關資源

- [快速開始](./getting-started.md)
- [架構指南](./architecture.md)
- [工具開發指南](./tool-development-guide.md)
- [Markdown 技能指南](./markdown-skills-guide.md)
- [模型供應商](./model-providers.md)
- [CronJob 與自動化](./cronjobs-automation.md)
- [安全性](./security.md)
- [API 參考](./api-reference.md)
