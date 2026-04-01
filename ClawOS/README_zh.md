# ClawOS

[English](README.md)

一套以 .NET 10 打造的 AI Agent 作業系統，採用 Clean Architecture（DDD、CQRS），具備多用戶工作空間隔離、分散式排程，以及模組化的 Tool / Skill / Channel 插件系統。

## 功能特色

- **Clean Architecture** — 分層架構，關注點分離清晰（Api / Application / Domain / Infrastructure）
- **領域驅動設計 (DDD)** — Entity、Value Object、Aggregate Root、Domain Event
- **CQRS 模式** — 使用 Mediator 實現命令查詢職責分離
- **Agent Pipeline** — 基於 Middleware 的 LLM 呼叫鏈（錯誤處理、日誌、逾時、秘密遮蔽）
- **Tool 插件系統** — 透過組件掃描自動註冊 C# Tools（`AgentToolBase<TArgs>`）
- **Markdown Skills** — 宣告式 `SKILL.md` 定義，組合 Tool 與 LLM 指令
- **多 Channel** — Web UI (SSE)、Telegram Bot，可擴充的 Adapter 模式（`IChannelAdapter`）
- **分散式 CronJob** — NATS JetStream + Leader Election 排程執行
- **雙層 Model Provider** — 全域（SuperAdmin）+ 每用戶各自的 LLM Provider
- **多用戶隔離** — 每用戶獨立工作空間、EF Core Query Filter、加密設定儲存
- **RBAC** — User / Admin / SuperAdmin，細粒度權限控制
- **NATS 訊息** — 雙 Broker 架構（Protobuf broker + JSON bus）
- **分散式快取** — 基於 NATS KV 的 `IDistributedCache`
- **物件儲存** — NATS Object Store 存放二進位檔案
- **SAGA 模式** — 分散式交易編排與補償機制
- **可觀測性** — OpenTelemetry 追蹤與指標（Prometheus + Grafana）
- **稽核日誌** — 安全關鍵操作的持久化稽核記錄
- **安全性** — JWT、AES-256 加密、CSP、登入限流、路徑穿越防護

## 預覽

### 開發者 UI
開發者友善介面，包含 Swagger UI、Wedally UI 與 Wiki：
![homepage](resources/homepage.png)

### 預設置的 Swagger UI
預先配置好的 Swagger UI，包含 Grouping、Tags、SecurityRequirement 設定：
![swagger](resources/swagger.png)

### NATS 端點 UI (Wedally UI)
類似 Swagger 的 NATS 端點操作介面：
![wedally](resources/wedally.png)
![wedally_req](resources/wedally_req.png)

### 自動產生的 Wiki 頁面
自動將 `docs/wiki/{en,zh}` 路徑下的文章轉成靜態網頁，支援 Markdown 格式渲染：
![wiki](resources/wiki.png)

## 專案結構

```
ClawOS/
├── src/
│   ├── ClawOS.Api/                       # ASP.NET Core API + 靜態前端
│   ├── ClawOS.Application/               # 業務邏輯、Agent Pipeline、CronJob 執行器
│   ├── ClawOS.Contracts/                 # 介面、DTO、Tool/Skill/Channel 契約
│   ├── ClawOS.Domain/                    # 領域實體
│   ├── ClawOS.Infrastructure/            # EF Core、安全、持久化
│   ├── ClawOS.Infrastructure.Llm.OpenAI/ # OpenAI LLM Provider
│   ├── ClawOS.Infrastructure.Llm.Ollama/ # Ollama LLM Provider
│   ├── ClawOS.Hosting/                   # DI 組裝、服務註冊
│   ├── ClawOS.Channels.Telegram/         # Telegram Bot Channel Adapter
│   ├── ClawOS.Cli/                       # CLI 介面
│   └── tools/                            # 內建 Tool 插件
│       ├── ClawOS.Tools.FileSystem/
│       ├── ClawOS.Tools.Shell/
│       ├── ClawOS.Tools.Git/
│       ├── ClawOS.Tools.GitHub/
│       ├── ClawOS.Tools.AzureDevOps/
│       ├── ClawOS.Tools.Http/
│       ├── ClawOS.Tools.WebSearch/
│       ├── ClawOS.Tools.Notion/
│       ├── ClawOS.Tools.Pdf/
│       ├── ClawOS.Tools.ImageGen/
│       ├── ClawOS.Tools.Tmux/
│       └── ClawOS.Tools.Preference/
├── skills/                               # Markdown 技能定義
├── tests/
│   ├── ClawOS.Api.IntegrationTests/
│   ├── ClawOS.Application.UnitTests/
│   ├── ClawOS.Domain.UnitTests/
│   ├── ClawOS.Infrastructure.UnitTests/
│   └── ClawOS.TestCommon/
├── docs/                                 # Wiki 文件
├── docker-compose.yml
└── Dockerfile
```

## 快速開始

### 先決條件

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker & Docker Compose

### 使用 Docker Compose 執行（推薦）

```bash
cp .env.example .env  # 編輯設定值
docker compose up -d
```

| 服務 | URL |
|------|-----|
| Web UI | http://localhost:5001 |
| Swagger UI | http://localhost:5001/swagger |
| SearXNG | http://localhost:8080 |
| PostgreSQL | localhost:5433 |

### 本機開發

```bash
# 啟動基礎設施服務
docker compose up -d postgres nats-broker nats-bus searxng

# 執行 API
dotnet run --project src/ClawOS.Api
```

### 資料庫遷移

遷移會在啟動時自動套用。若需建立新遷移：

```bash
dotnet ef migrations add MigrationName \
  --project src/ClawOS.Infrastructure \
  --startup-project src/ClawOS.Api
```

## 建立擴充

### Tool（C#）

Tool 提供可執行的能力（檔案讀寫、API 呼叫、Shell 指令等）：

```csharp
public class MyTool(IServiceProvider sp) : AgentToolBase<MyToolArgs>
{
    public override string Name => "my_tool";
    public override string Description => "這個工具做什麼";

    public override async Task<ToolResult> ExecuteAsync(
        MyToolArgs args, ToolContext context, CancellationToken ct)
    {
        return ToolResult.Success("結果");
    }
}

public record MyToolArgs(
    [property: Description("參數說明")]
    string? Parameter
);
```

放在 `src/tools/` 下 — 啟動時透過組件掃描自動註冊。

### Skill（Markdown）

Skill 組合 Tool 與 LLM 指令：

```markdown
---
name: my-skill
description: 這個技能做什麼
tools:
  - shell
  - read_file
---

## Instructions

你是一個有用的助手...
```

放在 `skills/` 下 — 在聊天中透過 `@my-skill` 觸發，或在 CronJob context 中引用。

### Channel Adapter

實作 `IChannelAdapter` + `IHostedService` 來橋接外部通訊平台：

```csharp
public interface IChannelAdapter
{
    string Name { get; }
    string DisplayName { get; }
    ChannelAdapterStatus Status { get; }
    Task SendMessageAsync(string externalId, string message, CancellationToken ct);
}
```

## API 概覽

### 聊天與對話
- `POST /api/v1/chat/stream` — 串流聊天回應 (SSE)
- `GET/POST/DELETE /api/v1/conversation` — 管理對話

### Model Provider
- `GET/POST/PUT/DELETE /api/v1/model-provider` — 全域 Provider（SuperAdmin）
- `GET/POST/PUT/DELETE /api/v1/user-model-provider` — 每用戶 Provider

### CronJob
- `GET/POST/PUT/DELETE /api/v1/cron-job` — 管理排程任務
- `POST /api/v1/cron-job/{id}/execute` — 手動觸發

### 設定
- `GET/PUT/DELETE /api/v1/user-config/{key}` — 每用戶加密設定
- `GET/PUT/DELETE /api/v1/app-config/{key}` — 全域應用設定（SuperAdmin）

### 使用者管理
- `POST /api/v1/auth/login` — 登入
- `POST /api/v1/auth/register` — 註冊
- `GET/POST /api/v1/user-management` — 使用者管理（SuperAdmin）

## NATS 整合

ClawOS 使用雙 NATS Broker：
- **Broker**（port 4222）：Protobuf 序列化，LLM 協調
- **Bus**（port 4223）：JSON 序列化，事件分發，CronJob 派送

### EventController 模式

```csharp
[ApiVersion("1")]
public class MyEventController : EventController
{
    [Subject("[controller].v{version:apiVersion}.{id}.get")]
    public async Task<MyResponse> GetById(int id)
    {
        var result = await Mediator.Send(new GetByIdQuery(id));
        return new MyResponse(result.Value);
    }
}
```

## 設定

### 環境變數

| 變數 | 說明 |
|------|------|
| `JWT_SECRET` | JWT 簽章金鑰（>= 32 字元）|
| `CLAWOS_ENCRYPTION_KEY` | AES-256 加密金鑰 |
| `LLM_PROVIDER` | 預設 Provider（`ollama` / `openai`）|
| `OPENAI_API_KEY` | OpenAI API Key |
| `OLLAMA_URL` | Ollama 伺服器 URL |
| `SEARXNG_URL` | SearXNG 搜尋引擎 URL |

## 測試

```bash
dotnet test
```

### 測試類型
- **領域單元測試** — 測試領域實體和值物件
- **應用層單元測試** — 測試處理器和 Pipeline 行為
- **基礎設施單元測試** — 測試儲存庫和持久化
- **整合測試** — 端對端 API 測試

## 技術選型

| 層級 | 技術 |
|------|------|
| 後端 | .NET 10、ASP.NET Core、EF Core、Mediator (CQRS) |
| 資料庫 | PostgreSQL（自動遷移）|
| 訊息 | NATS JetStream（雙 Broker）|
| 搜尋 | SearXNG |
| Channel | Web UI (SSE)、Telegram Bot |
| 前端 | Vanilla JS（CSP-compliant）、marked.js、highlight.js、KaTeX |
| 安全性 | JWT、AES-256、CSP、稽核日誌、登入限流 |
| 可觀測性 | OpenTelemetry、Prometheus、Grafana |
| 容器 | Docker、Docker Compose |
| 框架 | Weda.Core（DDD、CQRS、SAGA、分散式快取）|

## 授權條款

MIT
