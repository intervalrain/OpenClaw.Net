# PRD：C# Agent Platform（OpenClaw.NET）

## 1. 產品背景與目標

### 1.1 背景

隨著 LLM 與 Tool Calling 成熟，AI Agent 已從「聊天機器人」進化為可執行實際任務的 Agent Runtime。現有 OpenClaw（Node.js / TypeScript）雖功能完整，但在 **強型別、安全性、長期運行穩定度、企業級擴展性** 上存在限制。

本專案目標是使用 **C# / .NET** 重建一套 **OpenClaw.NET Agent Platform**，專注於工程品質、可維護性與長期演進能力。

---

## 2. 產品定位

### 2.1 產品一句話描述

> 一個以 C#/.NET 打造的「可插拔、事件驅動、強型別」AI Agent Runtime，可透過多種 Channel 操作並安全執行真實世界任務。

### 2.2 目標使用者

* 高階軟體工程師 / 平台工程師
* 企業內部 AI / Agent 平台團隊
* 想自託管 Agent、避免 SaaS Lock-in 的團隊
* 高資安需求的用戶場景

### 2.3 非目標

* 一般消費級聊天機器人
* 純 Prompt 工具（無實際任務執行）

---

## 3. 核心設計原則

1. **Strongly Typed First**（所有 Tool / Skill 皆可編譯期檢查）
2. **Event-Driven & Pipeline-based**
3. **Plugin-oriented Architecture**
4. **LLM Provider Agnostic**
5. **Secure by Default**（權限、Sandbox、限制）

---

## 4. 系統總體架構

### 4.1 高階架構

```
[ Channel Adapter ]
        ↓
[ Agent Gateway ]
        ↓
[ Agent Pipeline (Middleware) ]
        ↓
[ LLM Invoker ]
        ↓
[ Skill / Plugin Executor ]
        ↓
[ State / Memory / Storage ]
```

---

## 5. 核心模組與功能需求

### 5.1 Channel Adapter

#### 功能

* 接收外部輸入並轉為統一 AgentEvent

#### MVP 支援

* CLI
* WebSocket

#### 擴充目標

* Telegram
* Line

---

### 5.2 Agent Gateway

#### 功能

* Event routing
* Session / Conversation 管理
* Context 組裝

#### 需求

* 支援多 Agent Instance
* 支援並行請求

---

### 5.3 Agent Pipeline（Middleware）

#### 設計

```csharp
IAgentPipeline
  .Use<Auth>()
  .Use<RateLimit>()
  .Use<PromptGuard>()
  .Use<LLMInvoker>()
  .Use<SkillExecutor>();
```

#### MVP Middleware

* Logging
* Error Handling
* Timeout

---

### 5.4 LLM Abstraction Layer

#### 功能

* 封裝不同 LLM Provider
* 支援 Tool Calling

#### MVP Provider

* OpenAI

#### 擴充

* Azure OpenAI
* Anthropic
* Local LLM

---

### 5.5 Skill / Plugin System

#### Skill 定義

```csharp
public interface IAgentSkill
{
    string Name { get; }
    string Description { get; }
    Task<SkillResult> ExecuteAsync(SkillContext ctx);
}
```

#### Plugin 機制

* DLL 動態載入（AssemblyLoadContext）
* Attribute-based discovery
* Config 啟用/停用

#### MVP Skills

* FileSystem
* Shell (受限)
* HTTP Client

---

### 5.6 Memory / State

#### 功能

* Conversation memory
* Agent state

#### MVP

* In-memory

#### 擴充

* SQLite
* Redis

---

### 5.7 Security

#### 需求

* Skill Permission
* Tool Allowlist
* Execution Timeout

#### 非目標（第一版）

* OS-level Sandbox

---

## 6. 非功能性需求（NFR）

| 類型   | 需求              |
| ---- | --------------- |
| 穩定性  | 可長時間運行          |
| 可觀測性 | Structured logs |
| 擴展性  | Plugin 不影響核心    |
| 可測試性 | 核心可單元測試         |

---

## 7. 技術選型

| 層級      | 技術                  |
| ------- | ------------------- |
| Runtime | .NET 8/9            |
| Web     | ASP.NET Core        |
| CLI     | System.CommandLine  |
| JSON    | System.Text.Json    |
| Plugin  | AssemblyLoadContext |
| LLM     | OpenAI .NET SDK     |

---

## 8. 開發里程碑

### Phase 1：Agent Core (2 週)

* CLI
* LLM Tool Calling
* 內建 Skills

### Phase 2：Pipeline & Plugin (2 週)

* Middleware
* Plugin Loader

### Phase 3：Channel & Memory (2 週)

* WebSocket
* Memory Provider

---

## 9. 成功指標（Success Metrics）

* 可在 CLI 完成複雜任務（多 tool）
* 新增 Skill 不需改核心程式
* Agent 可連續運行 > 24h

---

## 10. 未來展望

* Agent-to-Agent 通訊（NATS）
* 分散式 Agent Runtime
* Web UI / Dashboard
* Enterprise Policy Engine

---

## 11. 開放問題

* Plugin 安全邊界策略
* Tool 自動生成 Schema 標準
* Memory 壓縮策略

---

**本 PRD 為工程導向文件，可直接作為實作藍本。**
