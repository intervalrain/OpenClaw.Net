# OpenClaw.Net Development Progress

## 最近完成的功能

### 1. Skills 系統完整實作 ✅

#### 1.1 Skills Auto-Registration (Assembly Scanning)
- ✅ 實作 `SkillRegistry` 自動掃描並註冊所有 `IAgentSkill` 實作
- ✅ 在 `ServiceCollectionExtensions` 註冊為 Singleton
- ✅ Skills 在啟動時自動載入，無需手動註冊

#### 1.2 Skills Settings (資料庫持久化)
- ✅ 新增 `SkillSetting` entity 到 Domain layer
- ✅ 實作 `ISkillSettingsService` 和 `SkillSettingsService`
- ✅ 建立 `SkillSettingsController` 提供 REST API
  - `GET /api/v1/skill-settings` - 列出所有 skills 及其啟用狀態
  - `POST /api/v1/skill-settings/{skillName}/enable` - 啟用 skill
  - `POST /api/v1/skill-settings/{skillName}/disable` - 停用 skill
- ✅ EF Core migration 建立 `skill_settings` table

#### 1.3 Slash Command 支援
- ✅ 實作 `SlashCommandParser` 解析 `/skill_name args` 格式
- ✅ 在 `IAgentPipeline` 新增 `ExecuteSkillDirectlyStreamAsync` 方法
- ✅ 在 `ChatController` 偵測 slash command 並直接執行 skill
- ✅ 支援參數自動轉換為 JSON 格式
- ✅ 檢查 skill 是否啟用才允許執行

#### 1.4 前端 Skills Settings UI
- ✅ 在 Settings Modal 加入 Skills 區塊
- ✅ 顯示所有 skills 及其描述
- ✅ Toggle switch 控制啟用/停用
- ✅ 即時更新狀態到後端
- ✅ 修復 Modal 滾動問題（加入 `overflow-y: auto`）

#### 1.5 Slash Command Autocomplete
- ✅ 實作 autocomplete dropdown UI
- ✅ 輸入 `/` 自動顯示可用 skills 列表
- ✅ 支援即時過濾（輸入 `/web` 過濾出 `web_search`）
- ✅ 鍵盤導航支援（↑/↓ 選擇，Tab/Enter 插入，Esc 關閉）
- ✅ 滑鼠點擊選擇支援
- ✅ 只顯示已啟用的 skills

### 2. 現有 Skills

目前已實作的 6 個基礎 skills：

| Skill | 描述 | 狀態 |
|-------|------|------|
| `http_request` | 發送 HTTP GET/POST 請求 | ✅ 啟用 |
| `write_file` | 寫入檔案 | ✅ 啟用 |
| `read_file` | 讀取檔案（過濾敏感檔案） | ✅ 啟用 |
| `list_directory` | 列出目錄內容 | ✅ 啟用 |
| `execute_command` | 執行 shell 命令（有安全限制） | ✅ 啟用 |
| `web_search` | 使用 SearXNG 搜尋網路 | ✅ 啟用 |
| `weather` | 查詢天氣和預報（wttr.in） | ✅ 啟用 |
| `github` | GitHub 操作（gh CLI: issues, PRs, CI） | ✅ 啟用 |

### 3. Telegram Channel 整合 ✅

- ✅ 實作 `OpenClaw.Channels.Telegram` 專案
- ✅ Telegram Bot Webhook 整合（`TelegramController`）
- ✅ 訊息處理流程（`HandleTelegramMessageCommandHandler`）
- ✅ Markdown 格式轉換（`TelegramMarkdownConverter`）
- ✅ 對話映射（`TelegramConversationMapper`）
- ✅ 支援 streaming 回覆
- ✅ 環境變數設定：`TELEGRAM__BOTTOKEN`

### 4. Image/Vision 支援 ✅

- ✅ OpenAI Vision API 整合（`OpenAILlmProvider` 使用 `ChatMessageContentPart`）
- ✅ `ChatMessage` 支援 `ImageContent` 和 `HasImages` 屬性
- ✅ `ChatRequest` 新增 `ChatImageAttachment` 參數
- ✅ `IAgentPipeline` 和 `AgentPipeline` 支援 `images` 參數
- ✅ 前端圖片上傳功能：
  - 剪貼簿貼上支援（Ctrl+V）
  - 拖放上傳支援
  - 檔案選擇按鈕
  - 圖片預覽和移除功能
  - 多圖片支援
- ✅ Base64 編碼和 MIME type 處理
- ✅ SSE streaming 保持圖片上下文

### 5. 技術架構改進

- ✅ Clean Architecture 設計（Domain → Application → Infrastructure → API）
- ✅ Skills 完全解耦，透過 `IAgentSkill` 介面統一管理
- ✅ 動態參數驗證（`ToolParameters` with JSON Schema）
- ✅ SSE (Server-Sent Events) 串流輸出
- ✅ Multi-Channel 架構（Web, Telegram）
- ✅ Docker Compose 完整基礎設施（PostgreSQL, NATS, SearXNG）
- ✅ OpenAI Vision API 整合（多模態支援）

---

## 待開發 Skills 清單

參考 [OpenClaw Skills](https://github.com/cased/openclaw/tree/main/skills)，以下是建議優先開發的 skills：

### 高優先級（按優先順序排列）

| Priority | Skill | 描述 | 需求 | 預估工時 |
|----------|-------|------|------|----------|
| ✅ ~~P0-1~~ | ~~**Weather**~~ | ~~查詢天氣和預報（wttr.in）~~ | ~~`curl`（已內建）~~ | ~~2-3h~~ |
| ✅ ~~P0-2~~ | ~~**GitHub**~~ | ~~GitHub 操作（issues, PRs, CI）~~ | ~~`gh` CLI~~ | ~~4-6h~~ |
| 🔴 P0-3 | **Git Operations** | 本地 git 操作（commit, branch, log） | `git`（已內建） | 3-4h |
| 🔴 P0-4 | **Azure DevOps** | Azure DevOps 操作（work items, PRs, pipelines） | `az devops` CLI / REST API | 6-8h |
| 🔴 P0-5 | **Image Generation** | OpenAI DALL-E 圖片生成 | OpenAI API Key | 4-5h |
| 🔴 P0-6 | **PDF Processing** | PDF 解析和處理 | `iTextSharp` / `PdfSharp` | 6-8h |
| 🔴 P0-7 | **Tmux Control** | Tmux session 管理 | `tmux` | 4-5h |
| 🔴 P0-8 | **Notion** | Notion API（頁面、資料庫管理） | API Key | 6-8h |

### 中優先級（待評估）

| Priority | Skill | 描述 | 需求 | 預估工時 |
|----------|-------|------|------|----------|
| 🟡 P1 | **Slack** | Slack 操作（訊息、反應、Pin） | Bot Token | 6-8h |
| 🟡 P1 | **Discord** | Discord bot 操作 | Bot Token | 5-6h |
| 🟡 P1 | **Trello** | Trello 看板管理 | API Key | 5-6h |
| 🟡 P1 | **Voice TTS** | 文字轉語音（Sherpa-ONNX） | ONNX 模型 | 8-10h |

### 低優先級（專案特定或進階功能）

| Priority | Skill | 描述 | 需求 | 預估工時 |
|----------|-------|------|------|----------|
| 🟢 P2 | **Obsidian** | Obsidian vault 管理 | Vault 路徑 | 5-6h |
| 🟢 P2 | **Apple Reminders** | macOS Reminders 整合 | macOS only | 6-8h |
| 🟢 P2 | **Apple Notes** | macOS Notes 整合 | macOS only | 5-6h |
| 🟢 P2 | **Spotify** | Spotify 播放控制 | `spotify_player` CLI | 4-5h |
| 🟢 P2 | **Video Frames** | 影片幀擷取 | `ffmpeg` | 5-6h |

---

## 建議開發順序（已按優先級調整）

### Phase 1: 基礎整合（第 1-2 週）
1. **Weather Skill** (P0-1) - 最簡單，立即可用，無需額外設定
2. **GitHub Skill** (P0-2) - 開發者必備工具
3. **Git Operations Skill** (P0-3) - 完善本地 Git 工作流程

**預估總工時**: 9-13 小時

### Phase 2: 企業協作工具（第 2-3 週）
4. **Azure DevOps Skill** (P0-4) - 企業級專案管理和 CI/CD
5. **Image Generation Skill** (P0-5) - AI 創意功能
6. **PDF Processing Skill** (P0-6) - 文件解析和處理

**預估總工時**: 16-21 小時

### Phase 3: 開發環境和知識管理（第 4 週）
7. **Tmux Control Skill** (P0-7) - Terminal session 管理
8. **Notion Skill** (P0-8) - 知識庫和資料庫整合

**預估總工時**: 10-13 小時

**Phase 1-3 總工時**: 35-47 小時（約 1 個月）

---

## 🔄 CQRS + Mediator 重構計畫

### 問題背景

目前系統使用 Mediator source generator，但遇到以下問題：

1. **跨 Assembly Handler 註冊問題**
   - Mediator source generator 只在有 `[assembly: MediatorOptions]` 的 assembly 中生成 handlers
   - `OpenClaw.Channels.Telegram` 的 `HandleTelegramMessageCommandHandler` 無法被 `OpenClaw.Application` 的 Mediator 識別
   - 目前 workaround：改用 `TelegramMessageService` 直接處理，繞過 Mediator

2. **Application 層混合模式**
   - 部分使用 CQRS (Commands/Queries + Handlers)：`Users/*`, `Auth/*`, `Setup/*`
   - 部分使用傳統 Service 類：`SkillSettingsService`, `SkillRegistry`, `LlmProviderFactory`, `AgentPipeline`
   - 導致程式碼風格不一致，難以統一使用 Mediator behaviors (logging, validation, etc.)

3. **架構不一致**
   - `OpenClaw.Application` 使用 Mediator pattern (Commands/Queries)
   - `OpenClaw.Channels.*` 直接使用 Service 類
   - 導致處理邏輯分散在不同層級

### 目前 Application 層分析

| 類別 | 模式 | 檔案 |
|------|------|------|
| **CQRS (✅)** | Commands/Queries | `Users/*`, `Auth/*`, `Setup/*` |
| **Service (❌)** | 傳統服務類 | `SkillSettingsService`, `SkillRegistry`, `LlmProviderFactory`, `AgentPipeline` |

**Service 類詳細分析：**

| Service | 職責 | 是否需轉 CQRS | 原因 |
|---------|------|---------------|------|
| `SkillSettingsService` | CRUD + 查詢 | ✅ 是 | 典型 CRUD，適合 Commands/Queries |
| `SkillRegistry` | 純讀取 (Registry) | ❌ 否 | Singleton 查表，無狀態變更 |
| `LlmProviderFactory` | Factory | ❌ 否 | Factory 模式，不適合 CQRS |
| `AgentPipeline` | 複雜業務邏輯 | ⚠️ 可選 | 可保留為 Domain Service |
| `SlashCommandParser` | 純工具 | ❌ 否 | 無狀態，純解析 |

### 重構目標

建立統一的 CQRS + Mediator 架構：
1. Application 層所有業務邏輯都透過 Commands/Queries 處理
2. 所有 Channel 都能透過 Mediator 派發命令
3. 統一使用 Mediator behaviors (logging, validation, authorization)

---

## Part 1: Application 層 Service → CQRS 重構

### 專案 CQRS 模式規範

**重要：Contracts vs Application 職責分離**

| 層級 | 職責 | 命名規範 |
|------|------|----------|
| `OpenClaw.Contracts` | API Controller / EventController 專用 | `*Request` / `*Response` |
| `OpenClaw.Application` | 內部 CQRS 邏輯 | `*Command` / `*Query` (與 Handler 同檔案) |

**Handler 返回值規範：統一使用 `ErrorOr<T>`**

所有 Handler 的返回類型必須是 `ErrorOr<T>`，讓 Controller 可透過繼承 `ApiController` 使用 FP-like `Match` function：

```csharp
// Controller 端 - 使用 Match 處理結果
[HttpPost]
public async Task<IActionResult> EnableSkill([FromRoute] string skillName)
{
    var result = await mediator.Send(new EnableSkillCommand(skillName));
    return result.Match(
        _ => NoContent(),    // Success case
        Problem);            // Error case - 自動轉換為適當的 HTTP status
}

// ApiController base class 提供 Problem method
// 自動將 ErrorOr errors 轉換為對應的 HTTP status codes:
// - ErrorType.Validation → 400 Bad Request
// - ErrorType.NotFound → 404 Not Found
// - ErrorType.Conflict → 409 Conflict
// - ErrorType.Unauthorized → 403 Forbidden
```

**正確的 CQRS 模式：Command/Query 與 Handler 寫在同一個 .cs 檔案內**

```
OpenClaw.Application/
├── Skills/
│   ├── Commands/
│   │   ├── EnableSkillCommandHandler.cs      # 包含 EnableSkillCommand record + Handler
│   │   └── DisableSkillCommandHandler.cs     # 包含 DisableSkillCommand record + Handler
│   ├── Queries/
│   │   ├── ListSkillSettingsQueryHandler.cs  # 包含 Query record + Handler
│   │   └── IsSkillEnabledQueryHandler.cs     # 包含 Query record + Handler
│   └── SkillRegistry.cs  # 保留 (Singleton 查表)
```

**範例：HandleTelegramMessageCommandHandler.cs (正確模式)**
```csharp
// Command record 與 Handler 在同一檔案
public record HandleTelegramMessageCommand(ChannelMessageReceivedEvent Event)
    : ICommand<ErrorOr<HandleTelegramMessageResult>>;

public record HandleTelegramMessageResult;

public class HandleTelegramMessageCommandHandler(...)
    : IRequestHandler<HandleTelegramMessageCommand, ErrorOr<HandleTelegramMessageResult>>
{
    public async ValueTask<ErrorOr<HandleTelegramMessageResult>> Handle(...) { ... }
}
```

### 需要重構的 Service: SkillSettingsService

**現狀：**
```csharp
// OpenClaw.Application/Skills/SkillSettingsService.cs
public class SkillSettingsService : ISkillSettingsService
{
    Task<List<SkillSettingDto>> GetListAsync();      // → Query
    Task<bool> IsEnabledAsync(string skillName);     // → Query
    Task EnableAsync(string skillName);              // → Command
    Task DisableAsync(string skillName);             // → Command
    Task<List<IAgentSkill>> GetEnabledSkillsAsync(); // → Query
}
```

**重構後：(Command/Query 都在 Handler 檔案內)**

```
OpenClaw.Application/
├── Skills/
│   ├── Commands/
│   │   ├── EnableSkillCommandHandler.cs      # EnableSkillCommand + Handler
│   │   └── DisableSkillCommandHandler.cs     # DisableSkillCommand + Handler
│   ├── Queries/
│   │   ├── ListSkillSettingsQueryHandler.cs  # ListSkillSettingsQuery + Handler
│   │   ├── IsSkillEnabledQueryHandler.cs     # IsSkillEnabledQuery + Handler
│   │   └── GetEnabledSkillsQueryHandler.cs   # GetEnabledSkillsQuery + Handler
│   └── SkillRegistry.cs  # 保留 (Singleton 查表)
```

**EnableSkillCommandHandler.cs 範例：**
```csharp
using ErrorOr;
using Mediator;

namespace OpenClaw.Application.Skills.Commands;

// Command record 與 Handler 在同一檔案
public record EnableSkillCommand(string SkillName) : ICommand<ErrorOr<Unit>>;

public class EnableSkillCommandHandler(
    ISkillSettingRepository repository,
    IUnitOfWork uow) : IRequestHandler<EnableSkillCommand, ErrorOr<Unit>>
{
    public async ValueTask<ErrorOr<Unit>> Handle(EnableSkillCommand request, CancellationToken ct)
    {
        var setting = await repository.GetByNameAsync(request.SkillName, ct);
        if (setting is null)
        {
            setting = new SkillSetting(request.SkillName, isEnabled: true);
            await repository.AddAsync(setting, ct);
        }
        else
        {
            setting.Enable();
        }
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
```

**ListSkillSettingsQueryHandler.cs 範例：**
```csharp
using ErrorOr;
using Mediator;

namespace OpenClaw.Application.Skills.Queries;

// Query record 與 Handler 在同一檔案
public record ListSkillSettingsQuery : IQuery<ErrorOr<List<SkillSettingDto>>>;

public class ListSkillSettingsQueryHandler(
    ISkillSettingRepository repository,
    ISkillRegistry registry) : IRequestHandler<ListSkillSettingsQuery, ErrorOr<List<SkillSettingDto>>>
{
    public async ValueTask<ErrorOr<List<SkillSettingDto>>> Handle(
        ListSkillSettingsQuery request, CancellationToken ct)
    {
        var skills = registry.GetAllSkills();
        var settings = await repository.GetAllAsync(ct);
        // ... mapping logic
        return skillDtos;
    }
}
```

### 舊版 CQRS 模式 (需逐步遷移)

目前 `Users/*`, `Auth/*`, `Setup/*` 仍使用舊模式：
- Commands 在 `OpenClaw.Contracts` (❌ 不正確，Contracts 應只有 Request/Response)
- Handlers 在 `OpenClaw.Application`

這些需要在後續重構中遷移到新模式。

### 保留的 Services (不需重構)

| Service | 原因 |
|---------|------|
| `SkillRegistry` | Singleton 查表，啟動時載入，無狀態變更 |
| `LlmProviderFactory` | Factory 模式，動態建立 Provider |
| `AgentPipeline` | 複雜的串流處理邏輯，不適合 Request/Response |
| `SlashCommandParser` | 純工具類，無副作用 |

### Part 1 實作步驟

| 步驟 | 任務 |
|------|------|
| 1.1 | `EnableSkillCommandHandler.cs` (Command + Handler) |
| 1.2 | `DisableSkillCommandHandler.cs` (Command + Handler) |
| 1.3 | `ListSkillSettingsQueryHandler.cs` (Query + Handler) |
| 1.4 | `IsSkillEnabledQueryHandler.cs` (Query + Handler) |
| 1.5 | `GetEnabledSkillsQueryHandler.cs` (Query + Handler) |
| 1.6 | 更新 `SkillSettingsController` 使用 Mediator |
| 1.7 | 更新 `TelegramMessageService` 使用 Mediator |
| 1.8 | 移除 `SkillSettingsService` 和 `ISkillSettingsService` |

---

## Part 2: Channel 層整合

### 方案比較

| 方案 | 優點 | 缺點 | 推薦度 |
|------|------|------|--------|
| **A. 統一 Application Layer** | 架構清晰、handlers 集中管理 | Channel 特定邏輯需抽象化 | ⭐⭐⭐⭐ |
| **B. 多 Mediator 實例** | 各 assembly 獨立、低耦合 | 複雜度高、難以跨 assembly 通訊 | ⭐⭐ |
| **C. 改用 MediatR** | 運行時註冊、靈活 | 需遷移現有 handlers、效能略差 | ⭐⭐⭐ |

### 推薦方案：A. 統一 Application Layer

#### Phase 1: 抽象化 Channel 介面

```
OpenClaw.Contracts/
├── Channels/
│   ├── IChannelMessageSender.cs      # 發送訊息介面
│   ├── IChannelTypingIndicator.cs    # 打字指示介面
│   └── ChannelMessageReceivedEvent.cs # 已存在
```

**IChannelMessageSender.cs**
```csharp
public interface IChannelMessageSender
{
    string ChannelName { get; }
    Task SendAsync(string chatId, string message, CancellationToken ct = default);
    Task SendTypingAsync(string chatId, CancellationToken ct = default);
}
```

#### Phase 2: 通用 Message Handler

將 Handler 移到 `OpenClaw.Application`，使用抽象介面：

```
OpenClaw.Application/
├── Channels/
│   ├── Commands/
│   │   └── HandleChannelMessageCommandHandler.cs  # Command + Handler 同檔案
│   └── Services/
│       └── ChannelMessageProcessor.cs
```

**HandleChannelMessageCommandHandler.cs (Command + Handler 同檔案)**
```csharp
public record HandleChannelMessageCommand(
    ChannelMessageReceivedEvent Event,
    string ChannelName) : ICommand<ErrorOr<Unit>>;

public class HandleChannelMessageCommandHandler(...)
    : IRequestHandler<HandleChannelMessageCommand, ErrorOr<Unit>>
{
    // 處理邏輯
}
```

#### Phase 3: Channel 實作介面

```
OpenClaw.Channels.Telegram/
├── Services/
│   └── TelegramMessageSender.cs  # 實作 IChannelMessageSender
├── EventControllers/
│   └── TelegramEventController.cs  # 只負責轉發到 Mediator
```

#### Phase 4: DI 註冊

```csharp
// TelegramServiceCollectionExtensions.cs
services.AddScoped<IChannelMessageSender, TelegramMessageSender>();

// Program.cs - Mediator 只需 Application assembly
services.AddMediator(options =>
{
    options.Assemblies = [typeof(IApplicationMarker).Assembly];
});
```

### 實作步驟

| 步驟 | 任務 | 預估工時 |
|------|------|----------|
| 1 | 建立 `IChannelMessageSender` 介面 | 0.5h |
| 2 | 建立通用 `HandleChannelMessageCommand` | 1h |
| 3 | 將 `TelegramMessageService` 邏輯遷移到 Handler | 2h |
| 4 | 實作 `TelegramMessageSender` | 1h |
| 5 | 更新 `TelegramEventController` 使用 Mediator | 0.5h |
| 6 | 移除舊的 `HandleTelegramMessageCommandHandler` | 0.5h |
| 7 | 測試和修復 | 1h |
| 8 | 更新文件 | 0.5h |

**總預估工時**: 7-8 小時

### 遷移後架構

```
┌─────────────────────────────────────────────────────────────┐
│                     OpenClaw.Api                             │
│  ┌──────────────────┐  ┌──────────────────────────────────┐ │
│  │ ChatController   │  │ TelegramEventController          │ │
│  │ (Web Channel)    │  │ (JetStream Consumer)             │ │
│  └────────┬─────────┘  └─────────────┬────────────────────┘ │
│           │                          │                       │
│           ▼                          ▼                       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              IMediator.Send(HandleChannelMessageCmd)   │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  OpenClaw.Application                        │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         HandleChannelMessageCommandHandler             │ │
│  │  ┌──────────────────────────────────────────────────┐  │ │
│  │  │ 1. Load conversation                             │  │ │
│  │  │ 2. Parse slash commands                          │  │ │
│  │  │ 3. Execute Agent Pipeline                        │  │ │
│  │  │ 4. Send response via IChannelMessageSender       │  │ │
│  │  └──────────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  OpenClaw.Contracts                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ IChannelMessageSender                                │   │
│  │ - SendAsync(chatId, message)                         │   │
│  │ - SendTypingAsync(chatId)                            │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ Telegram        │ │ Discord         │ │ Line            │
│ MessageSender   │ │ MessageSender   │ │ MessageSender   │
│ (ITelegramBot)  │ │ (IDiscordClient)│ │ (ILineMessaging)│
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

### 優先級

🔴 **高優先級** - 在新增更多 Channel 之前完成

建議在實作 Discord、Line 等其他 Channel 之前先完成此重構，避免重複相同的 workaround。

---

## 技術債務和改進項目

### 需要修復
- ⚠️ CQRS + Mediator 跨 Assembly 問題（見上方重構計畫）

### 可選優化
- 🔧 加入 Skill 版本管理
- 🔧 Skill 執行統計和監控（已有 Grafana 基礎）
- 🔧 Skill 權限系統（限制某些 skills 只能特定用戶使用）
- 🔧 Skill 參數驗證增強（更詳細的錯誤訊息）
- 🔧 Skill 測試覆蓋率提升

---

## 下一步行動

### 立即可做
1. ✅ **Weather Skill 完成** (P0-1) - 使用 `wttr.in` API
2. ✅ **GitHub Skill 完成** (P0-2) - 使用 `gh` CLI + `GH_TOKEN` 環境變數
3. **開始實作 Git Operations Skill** (P0-3)
   - 本地 git 操作（commit, branch, log, diff）
   - 預估工時: 3-4 小時

### 技術準備事項
- **Azure DevOps Skill** 需要:
  - Azure DevOps PAT (Personal Access Token)
  - 或使用 `az devops` CLI extension
- **Image Generation** 需要:
  - OpenAI API Key（已有 Model Provider 系統可複用）
- **PDF Processing** 需要:
  - 選擇 .NET PDF 庫: `iTextSharp`, `PdfSharp`, 或 `Docnet.Core`
- **Notion** 需要:
  - Notion Integration API Key

### 需要決策
- ~~確認優先開發哪些 skills~~ ✅ **已確認**
- Azure DevOps 使用 CLI 還是 REST API？（建議：REST API 更靈活）
- PDF 處理庫選擇？（建議：`iTextSharp` 功能最完整）
- 是否需要 Skill marketplace 機制？（可延後到 Phase 4）
- Multi-tenant 支援？（可延後，目前 single-user 即可）

---

**更新時間**: 2026-03-06
**狀態**: Skills 系統核心功能完成，Telegram Channel 整合完成，Image/Vision 支援完成，準備擴充更多 skills
