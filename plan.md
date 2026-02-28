# Telegram 整合功能實作計畫

## 概述

為 OpenClaw.Net 新增 Telegram Bot Channel Adapter，讓使用者可以透過 Telegram 與 AI Agent 對話。此功能對應 PRD 中的 **5.1 Channel Adapter** 及 **US-01 多 Channel 對話整合**。

---

## 架構定位

### Telegram 是 Presentation Layer

參照目前的分層架構：

```
Presentation Layer     ← OpenClaw.Api (Web UI / REST / SSE)
                       ← OpenClaw.Cli (CLI)
                       ← OpenClaw.Channels.Telegram  ← 新增（與 Api 同層）

Application Layer      ← OpenClaw.Application (Pipeline, Skills logic)
Contracts Layer        ← OpenClaw.Contracts (IChannelAdapter, IAgentPipeline, ...)
Domain Layer           ← OpenClaw.Domain (Conversation, Message entities)
Infrastructure Layer   ← OpenClaw.Infrastructure (DB, LLM providers)
```

Telegram 與 `OpenClaw.Api` 同屬 Presentation Layer，差別在於：
- `OpenClaw.Api`：透過 HTTP REST + SSE 接收/回應
- `OpenClaw.Channels.Telegram`：透過 Telegram Bot API 接收/回應
- 兩者都往下呼叫同一個 `IAgentPipeline`

### 單一 Host 啟動所有服務

所有東西在同一個 `WebApplication` Host 中啟動：

```
OpenClaw.Api (Program.cs)
├── ASP.NET Core Web Server    → 網頁 + REST API
├── BackgroundServices         → 現有背景服務
└── Channel Adapters           → Telegram (BackgroundService + IChannelAdapter)
                               → 未來: Line, Discord...
```

Channel Adapter 以 `IHostedService` 形式註冊，隨 Host 一起啟動/停止。

---

## IChannelAdapter 抽象設計（Contracts Layer）

### 新增檔案：`OpenClaw.Contracts/Channels/`

#### IChannelAdapter.cs

```csharp
namespace OpenClaw.Contracts.Channels;

/// <summary>
/// 定義 Channel Adapter 的通用介面。
/// 每個 Channel（Telegram, Line, Discord...）都實作此介面。
/// 實作類別應同時實作 IHostedService 以便隨 Host 啟停。
/// </summary>
public interface IChannelAdapter
{
    /// <summary>唯一識別名稱（如 "telegram", "line"）</summary>
    string Name { get; }

    /// <summary>顯示名稱（如 "Telegram Bot"）</summary>
    string DisplayName { get; }

    /// <summary>目前狀態</summary>
    ChannelAdapterStatus Status { get; }

    /// <summary>
    /// 主動推送訊息到外部 Channel（用於通知場景，如 US-05 進度通知）。
    /// </summary>
    /// <param name="externalId">外部使用者/群組 ID（如 Telegram chatId）</param>
    /// <param name="message">訊息內容</param>
    /// <param name="ct">取消 token</param>
    Task SendMessageAsync(string externalId, string message, CancellationToken ct = default);
}
```

#### ChannelAdapterStatus.cs

```csharp
namespace OpenClaw.Contracts.Channels;

public enum ChannelAdapterStatus
{
    Disabled,
    Stopped,
    Starting,
    Running,
    Error
}
```

#### ChannelMessage.cs

```csharp
namespace OpenClaw.Contracts.Channels;

/// <summary>
/// Channel Adapter 轉換後的統一訊息格式。
/// 各 Channel Adapter 將外部訊息轉為此格式後交給 Pipeline 處理。
/// </summary>
public record ChannelMessage(
    string ChannelName,         // "telegram", "line"
    string ExternalUserId,      // Telegram chatId, Line userId
    string? ExternalUserName,   // 使用者顯示名稱
    string Content,             // 訊息文字
    ChannelMessageType Type = ChannelMessageType.Text);

public enum ChannelMessageType
{
    Text,
    Image,
    File,
    Command
}
```

### 設計原則

1. **IChannelAdapter 不繼承 IHostedService**
   - `OpenClaw.Contracts` 不依賴 `Microsoft.Extensions.Hosting`
   - 實作類別自行同時實作 `BackgroundService` + `IChannelAdapter`

2. **SendMessageAsync 支援主動推送**
   - 對應 US-05：Agent 完成任務後主動透過 Telegram 通知使用者
   - 未來 Cron Job 結果推送也使用此機制

3. **ChannelMessage 作為統一訊息格式**
   - 各 Adapter 負責將外部格式（Telegram Update, Line Event）轉為 `ChannelMessage`
   - 未來如需統一 routing layer 可從此擴展

---

## 整體訊息流程

```
Telegram Server
    │
    ↓ Polling / Webhook
TelegramChannelAdapter (BackgroundService + IChannelAdapter)
    │
    ↓ 轉換為 ChannelMessage
TelegramMessageHandler
    │
    ├─ 查找/建立 Conversation (via TelegramConversationMapper)
    ├─ 檢查 SlashCommand (via ISlashCommandParser)
    │
    ↓ 呼叫
IAgentPipeline.ExecuteStreamAsync()
    │
    ↓ 串流收集回應
TelegramMessageHandler
    │
    ↓ Markdown 轉換 + 長訊息分割 + 發送
ITelegramBotClient.SendMessage()
    │
    ↓
Telegram Server → 使用者
```

---

## 專案結構

```
OpenClaw/
├── src/
│   ├── OpenClaw.Contracts/
│   │   └── Channels/                          ← 新增
│   │       ├── IChannelAdapter.cs
│   │       ├── ChannelAdapterStatus.cs
│   │       └── ChannelMessage.cs
│   │
│   └── OpenClaw.Channels.Telegram/            ← 新增專案
│       ├── OpenClaw.Channels.Telegram.csproj
│       ├── TelegramBotOptions.cs              # 設定 (bot token, webhook, 白名單)
│       ├── TelegramChannelAdapter.cs          # BackgroundService + IChannelAdapter
│       ├── TelegramMessageHandler.cs          # 訊息處理核心 (建立 Scope 呼叫 Pipeline)
│       ├── TelegramConversationMapper.cs      # chatId ↔ Conversation 映射
│       ├── TelegramMarkdownConverter.cs       # Markdown → Telegram MarkdownV2
│       ├── TelegramWebhookController.cs       # Webhook endpoint (可選)
│       └── ServiceCollectionExtensions.cs     # DI: AddTelegramChannel()
```

---

## 實作步驟

### Step 1：Contracts 層 - IChannelAdapter 抽象

**修改專案：** `OpenClaw.Contracts`

- 新增 `Channels/IChannelAdapter.cs`
- 新增 `Channels/ChannelAdapterStatus.cs`
- 新增 `Channels/ChannelMessage.cs`
- 不需要額外 NuGet 依賴

### Step 2：建立 Telegram 專案 + 設定

**新建專案：** `src/OpenClaw.Channels.Telegram/`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Telegram.Bot" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\OpenClaw.Contracts\OpenClaw.Contracts.csproj" />
    <ProjectReference Include="..\OpenClaw.Application\OpenClaw.Application.csproj" />
  </ItemGroup>
</Project>
```

**TelegramBotOptions.cs：**
```csharp
public class TelegramBotOptions
{
    public const string SectionName = "Telegram";

    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }        // 有值用 Webhook，空值用 Polling
    public string? SecretToken { get; set; }        // Webhook 驗證
    public long[]? AllowedUserIds { get; set; }     // 白名單（空 = 允許所有人）
}
```

**修改：** `OpenClaw.Hosting.csproj` 加入對 Telegram 專案的參考

**修改：** `appsettings.json` 新增 Telegram 區塊

### Step 3：TelegramChannelAdapter（核心生命週期）

```csharp
public class TelegramChannelAdapter : BackgroundService, IChannelAdapter
{
    public string Name => "telegram";
    public string DisplayName => "Telegram Bot";
    public ChannelAdapterStatus Status { get; private set; }

    // StartAsync:
    //   - if !Enabled → Status = Disabled, return
    //   - if WebhookUrl 有值 → SetWebhookAsync()
    //   - else → StartReceiving() (polling)
    //   - Status = Running

    // ExecuteAsync (polling mode):
    //   - TelegramBotClient.StartReceiving() 接收 Updates
    //   - 每個 Update 委派給 TelegramMessageHandler

    // StopAsync:
    //   - DeleteWebhookAsync() (if webhook mode)
    //   - Status = Stopped

    // IChannelAdapter.SendMessageAsync:
    //   - botClient.SendMessage(chatId, message, parseMode: MarkdownV2)
}
```

**Webhook vs Polling：**
- 預設 **Polling 模式**（開發簡單、不需公開 URL）
- 設定了 `WebhookUrl` 則自動切換 **Webhook 模式**（生產環境）

### Step 4：TelegramMessageHandler

```csharp
public class TelegramMessageHandler(
    IServiceScopeFactory scopeFactory,
    ITelegramBotClient botClient,
    TelegramConversationMapper conversationMapper,
    ISlashCommandParser slashCommandParser,
    ISkillRegistry skillRegistry,
    ISkillSettingsService skillSettingsService,
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramMessageHandler> logger)
{
    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        // 1. 過濾非文字訊息
        // 2. 驗證 AllowedUserIds 白名單
        // 3. 發送 ChatAction.Typing
        // 4. 處理 bot commands (/start, /new, /help, /skills)
        // 5. using var scope = scopeFactory.CreateScope();
        //    var pipeline = scope.ServiceProvider.GetRequiredService<IAgentPipeline>();
        //    var repo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        // 6. 查找/建立 Conversation (via conversationMapper)
        // 7. 載入對話歷史
        // 8. 檢查 slash command → 執行 skill
        // 9. pipeline.ExecuteStreamAsync() → 收集完整回應
        // 10. TelegramMarkdownConverter 轉換格式
        // 11. 分割長訊息（上限 4096 字元）
        // 12. SendMessage 回 Telegram
        // 13. 儲存對話到 DB
    }
}
```

**關鍵：** `IAgentPipeline` 是 **Scoped**，必須透過 `IServiceScopeFactory` 建立新 scope。

### Step 5：TelegramConversationMapper

```csharp
public class TelegramConversationMapper
{
    // ConcurrentDictionary<long, Guid> 快取 chatId → conversationId

    // GetOrCreateConversationAsync(chatId, username):
    //   1. 查快取
    //   2. 快取沒有 → 查 DB (Conversation title = "tg:{chatId}")
    //   3. DB 也沒有 → 建立新 Conversation
    //   4. 更新快取

    // ResetConversationAsync(chatId):
    //   移除快取，下次 GetOrCreate 建新的（for /new 指令）
}
```

### Step 6：TelegramMarkdownConverter

```csharp
public static class TelegramMarkdownConverter
{
    // 標準 Markdown → Telegram MarkdownV2
    // 跳脫特殊字元: _ * [ ] ( ) ~ ` > # + - = | { } . !
    // code block 和 inline code 內部不跳脫
    public static string ToTelegramMarkdownV2(string markdown);
}
```

### Step 7：Bot Commands

| 指令 | 功能 |
|------|------|
| `/start` | 歡迎訊息 + 使用說明 |
| `/new` | 建立新對話（reset conversation） |
| `/help` | 顯示可用指令 |
| `/skills` | 顯示已啟用的 Skills |
| `/skill_name args` | 複用現有 `ISlashCommandParser` 呼叫 OpenClaw Skill |

### Step 8：DI 整合

**ServiceCollectionExtensions.cs：**
```csharp
public static IServiceCollection AddTelegramChannel(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var section = configuration.GetSection(TelegramBotOptions.SectionName);
    if (!section.GetValue<bool>("Enabled"))
        return services;

    services.Configure<TelegramBotOptions>(section);
    services.AddSingleton<ITelegramBotClient>(sp => {
        var opts = sp.GetRequiredService<IOptions<TelegramBotOptions>>();
        return new TelegramBotClient(opts.Value.BotToken);
    });
    services.AddSingleton<TelegramConversationMapper>();
    services.AddSingleton<TelegramMessageHandler>();
    services.AddSingleton<TelegramChannelAdapter>();
    services.AddSingleton<IChannelAdapter>(sp => sp.GetRequiredService<TelegramChannelAdapter>());
    services.AddHostedService(sp => sp.GetRequiredService<TelegramChannelAdapter>());

    return services;
}
```

**Hosting 層呼叫：**
```csharp
// OpenClaw.Hosting/ServiceCollectionExtensions.cs
services.AddTelegramChannel(configuration);
```

### Step 9：Webhook Controller（可選，Webhook 模式才需要）

```csharp
[ApiController]
[Route("api/v1/telegram")]
[AllowAnonymous]
public class TelegramWebhookController : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromBody] Update update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken)
    {
        // 驗證 secretToken
        // Fire-and-forget 委派給 TelegramMessageHandler
        // 立即回傳 200 OK
    }
}
```

### Step 10：docker-compose 更新

```yaml
api:
  environment:
    - Telegram__Enabled=false
    - Telegram__BotToken=
    - Telegram__WebhookUrl=
    - Telegram__SecretToken=
```

---

## 安全考量

1. **AllowedUserIds 白名單** - 空 = 允許所有人；有值 = 只允許白名單
2. **Webhook SecretToken** - 驗證 request 來源
3. **Bot Token 不進 log** - 遵循 `SecretRedactionMiddleware` 模式
4. **Rate Limiting** - per chatId 簡易限流
5. **Skill 權限共用** - 與 Web UI 共用 `ISkillSettingsService`

---

## 測試策略

1. **單元測試：**
   - `TelegramMarkdownConverter` 格式轉換
   - `TelegramConversationMapper` 映射邏輯
   - `TelegramMessageHandler` (mock ITelegramBotClient + IAgentPipeline)

2. **手動測試：**
   - @BotFather 建測試 bot → Polling 模式測試
   - ngrok 測 Webhook 模式

---

## 預估工時

| # | 任務 | 預估 |
|---|------|------|
| 1 | Contracts: IChannelAdapter 抽象 | 0.5h |
| 2 | 建立 Telegram 專案 + Options | 0.5h |
| 3 | TelegramChannelAdapter (polling + lifecycle) | 1.5h |
| 4 | TelegramMessageHandler (核心邏輯) | 2h |
| 5 | TelegramConversationMapper | 1h |
| 6 | TelegramMarkdownConverter | 1h |
| 7 | Bot Commands (/start, /new, /help, /skills) | 1h |
| 8 | DI + Hosting 整合 | 0.5h |
| 9 | Webhook 模式 + Controller | 1h |
| 10 | 單元測試 | 2h |
| **合計** | | **~11h** |

---

## 未來擴展

- 圖片訊息支援（多模態 LLM）
- Inline Keyboard 互動式按鈕
- 主動推送通知（Cron Job、CI/CD → `IChannelAdapter.SendMessageAsync`）
- Line Channel Adapter（第二個 IChannelAdapter 實作）
- Channel Adapter 管理 API（列出所有 adapters、狀態查詢、啟停控制）
