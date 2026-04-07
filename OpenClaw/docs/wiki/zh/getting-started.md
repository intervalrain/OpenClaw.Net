---
sidebar_position: 2
sidebar_label: '快速開始'
hide_title: true
title: '快速開始'
keywords: ['OpenClaw', '安裝', 'Docker', '設定', '快速開始']
description: '如何安裝、設定與執行 OpenClaw.NET'
---

# 快速開始

> 幾分鐘內完成安裝、設定與執行

## 環境需求

- [Docker](https://www.docker.com/get-started) 與 Docker Compose
- [.NET 10 SDK](https://dotnet.microsoft.com/download)（僅本機開發需要）

## 使用 Docker Compose 執行

```bash
cd OpenClaw

# 設定機密
cp .env.example .env   # 編輯您的值

# 啟動所有服務
docker compose up -d
```

### 服務端點

| 服務 | URL |
|------|-----|
| Web UI | http://localhost:5001 |
| Swagger API 文件 | http://localhost:5001/swagger |
| Wiki | http://localhost:5001/wiki |
| SearXNG 搜尋 | http://localhost:8080 |

### 環境變數

在 `.env` 中設定的關鍵變數：

| 變數 | 說明 | 必要 |
|------|------|------|
| `JWT_SECRET` | JWT 簽章金鑰（>= 32 字元） | 是 |
| `OPENCLAW_ENCRYPTION_KEY` | AES-256 加密金鑰（用於靜態加密機密資料） | 是 |
| `LLM_PROVIDER` | 預設 LLM 供應商（`ollama` 或 `openai`） | 否 |
| `OPENAI_API_KEY` | OpenAI API Key | 使用 OpenAI 時 |
| `OLLAMA_URL` | Ollama 伺服器 URL（預設：`http://localhost:11434`） | 使用 Ollama 時 |

## 首次設定

1. 開啟 http://localhost:5001 — 將自動導向設定頁面
2. 建立初始 **SuperAdmin** 帳號
3. 前往 **Settings > Models** 設定您的 LLM 供應商
4. 開始聊天！

## 本機開發

僅啟動基礎設施服務：

```bash
cd OpenClaw

# 啟動 PostgreSQL、NATS 與 SearXNG
docker compose up -d postgres nats-broker nats-bus searxng

# 本機執行 API
dotnet run --project src/OpenClaw.Api
```

### 資料庫遷移

遷移會在啟動時**自動套用**。若要建立新的遷移：

```bash
cd OpenClaw
dotnet ef migrations add MigrationName \
  --project src/OpenClaw.Infrastructure \
  --startup-project src/OpenClaw.Api
```

### 執行測試

```bash
cd OpenClaw

# 所有測試
dotnet test

# 特定測試專案
dotnet test tests/OpenClaw.Application.UnitTests
dotnet test tests/OpenClaw.Domain.UnitTests
dotnet test tests/OpenClaw.Api.IntegrationTests
```

## Docker Compose 服務

`docker-compose.yml` 編排以下服務：

| 服務 | 用途 |
|------|------|
| `openclaw-api` | 主應用程式（API + 前端） |
| `postgres` | PostgreSQL 資料庫 |
| `nats-broker` | NATS JetStream 訊息佇列 |
| `nats-bus` | NATS 事件匯流排 |
| `searxng` | SearXNG 網頁搜尋引擎 |

## 下一步

- [架構指南](./architecture.md) — 了解系統設計
- [工具開發指南](./tool-development-guide.md) — 建立自訂 C# 工具
- [Markdown 技能指南](./markdown-skills-guide.md) — 建立宣告式技能
- [模型供應商](./model-providers.md) — 設定 LLM 供應商
