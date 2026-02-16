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

## 11. User Stories（未來功能）

### US-01：多 Channel 對話整合
**作為** 使用者，**我希望** 透過 Telegram 或 Line 與 OpenClaw 進行對話，**以便** 不需開啟電腦也能操作 Agent。

**Acceptance Criteria:**
- 支援 Telegram Bot Channel
- 支援 Line Messaging API Channel
- 訊息雙向同步，支援文字與圖片

---

### US-02：遠端桌面/瀏覽器操作可視化
**作為** Host 管理員，**我希望** 當程式運行在 Server 容器內時，可透過 SSH 看到 Server 的桌面動作或瀏覽器操作，**以便** 監控 Agent 的實際執行狀況。

**Acceptance Criteria:**
- 容器內運行 headless browser（Playwright/Puppeteer）
- 透過 VNC/noVNC 或 X11 forwarding 提供遠端可視化
- SSH tunnel 支援安全連線

---

### US-03：對話式 Cron Job 管理
**作為** 使用者，**我希望** 透過對話方式新增 Cron Jobs，**以便** 不需手動編輯設定檔即可排程任務。

**Acceptance Criteria:**
- 自然語言描述排程（如「每天早上 9 點執行 X」）
- 支援 list/add/remove/update cron jobs
- 持久化儲存 cron 設定

---

### US-04：Web 管理介面
**作為** 管理員，**我希望** 有一個網頁介面來管理 Config、Cron Jobs、API Keys、Models、Skills 等，同時支援網頁版對話，**以便** 集中管理所有設定與功能。

**Acceptance Criteria:**
- Web Dashboard：Config 管理
- Cron Jobs CRUD 介面
- API Key / LLM Model 設定
- Skills 啟用/停用管理
- 內建 Web Chat 介面

---

### US-05：24/7 自主開發模式
**作為** 開發者，**我希望** Agent 可以自主、不停歇的 24/7 進行程式開發，**以便** 持續推進專案進度。

**Acceptance Criteria:**
- Agent 具備 Task Queue 與優先序管理
- 自動 commit、push、建立 PR
- 錯誤自動恢復與重試機制
- 進度報告與通知（Telegram/Line/Email）

---

### US-06：瀏覽器自動化購物
**作為** 使用者，**我希望** Agent 可透過瀏覽器操作進行購物流程（瀏覽、加入購物車、填寫資料），支付動作仍由我執行，**以便** 節省重複操作時間。

**Acceptance Criteria:**
- Browser Skill 支援 Playwright/Puppeteer
- 從 ConfigStore 讀取使用者資料（姓名、地址、電話等）
- 自動填入表單欄位
- 停在支付頁面等待使用者確認
- 支援截圖回傳確認畫面

---

### US-07：Azure DevOps 整合與自動化
**作為** 開發者，**我希望** Agent 可透過 HTTP Request 讀取 Azure DevOps (ADO)，自動分析 User Story、更新 Tasks 狀態，**以便** 減少手動維護 ADO 的時間。

**Acceptance Criteria:**
- 從 ConfigStore 讀取 ADO PAT Token 與組織/專案設定
- 透過 ADO REST API 讀取 Work Items（User Stories、Tasks）
- 根據 ADO Task 對應的 project-path，掃描本地 Git commits
- 比對 codebase 變更與 Task 描述，判斷完成度
- 自動更新 ADO Task 狀態（New → Active → Closed）
- 針對 User Story 進行分析，自動拆解為 Tasks/Todos
- 新建立的 Tasks 同步推送至 ADO
- 支援雙向同步：本地 todo 檔案 ↔ ADO Work Items

**技術細節:**
- ADO Skill：封裝 ADO REST API 操作
- Git Skill：分析 commits、diff、branch 狀態
- 映射表：ADO Work Item ID ↔ Local Project Path
- 狀態推斷邏輯：根據 commit message 關鍵字或程式碼變更判斷

---

## 12. 開放問題

* Plugin 安全邊界策略
* Tool 自動生成 Schema 標準
* Memory 壓縮策略
* Browser Skill 安全性（防止敏感資料洩漏）
* 24/7 模式下的資源管理與成本控制
* ADO 同步衝突處理策略（本地 vs 遠端變更）
* Git commit message 與 ADO Task 的映射規則

---

**本 PRD 為工程導向文件，可直接作為實作藍本。**
