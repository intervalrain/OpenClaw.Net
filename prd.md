# PRD：ClawOS — AI Agent Operating System

## 1. 產品背景與目標

### 1.1 背景

隨著 LLM 與 Tool Calling 成熟，AI Agent 已從「聊天機器人」進化為可執行實際任務的自主系統。然而現有方案普遍面臨以下挑戰：

- **碎片化**：Chat、DevOps、IoT、排程分散在不同平台
- **缺乏標準化 Runtime**：Agent 沒有統一的執行環境與資源管理
- **安全性不足**：SaaS 方案資料無法留在企業內網
- **擴展性受限**：無法從 Cloud 延伸到 Edge Device

ClawOS 定位為 **AI Agent 的作業系統（Operating System）**，以 C#/.NET 打造，提供 Agent 執行、排程、通訊、裝置管理的統一 Runtime。

### 1.2 命名哲學

> **Claw**（爪）= Agent 與真實世界互動的能力  
> **OS**（Operating System）= 不只是應用程式，而是 Agent 的運行環境

---

## 2. 產品定位

### 2.1 產品一句話描述

> 一個以 C#/.NET 打造的 AI Agent Operating System：管理 Agent、Tool、Channel、Device，讓 AI 在 Cloud 與 Edge 上自主執行真實世界任務。

### 2.2 ClawOS 的 "OS" 類比

| OS 概念 | ClawOS 對應 |
|---------|------------|
| Process Management | Agent Pipeline + CronJob Scheduler |
| File System | Tools.FileSystem + Per-user Workspace Isolation |
| IPC (Inter-Process Communication) | NATS Messaging (Broker + Bus) |
| Device Drivers | Tool Plugins (Shell, Git, HTTP, MQTT...) |
| Networking | Channel Adapters (Telegram, MQTT, WebSocket) |
| User Space | Multi-user RBAC + UserPreference + Encrypted Config |
| Kernel | Weda.Core Framework (DDD, CQRS, Middleware Pipeline) |
| Scheduler | CronJob + Distributed Leader Election |
| Package Manager | Skills (SKILL.md) + Tool Auto-discovery |

### 2.3 目標使用者

* 高階軟體工程師 / 平台工程師
* 企業內部 AI / Agent 平台團隊
* IoT / Edge Computing 團隊
* 想自託管 Agent、避免 SaaS Lock-in 的團隊
* 高資安需求的用戶場景

### 2.4 非目標

* 一般消費級聊天機器人
* 純 Prompt 工具（無實際任務執行）

---

## 3. 核心設計原則

1. **OS-level Abstraction** — Agent、Tool、Channel、Device 皆為 OS 管理的資源
2. **Strongly Typed First** — 所有 Tool / Skill 皆可編譯期檢查
3. **Event-Driven & Pipeline-based** — NATS messaging + Middleware pipeline
4. **Plugin-oriented Architecture** — Tool、Channel、LLM Provider 皆可插拔
5. **LLM Provider Agnostic** — OpenAI、Ollama、Anthropic、自訂 endpoint
6. **Secure by Default** — 權限隔離、加密儲存、Sandbox 執行
7. **Cloud-to-Edge** — 同一套架構可部署在 Server 與 Edge Device

---

## 4. 系統總體架構

### 4.1 高階架構

```
┌─────────────────────────────────────────────────┐
│                   ClawOS Cloud                   │
│                                                  │
│  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │ Web UI   │  │ Telegram │  │  MQTT Bridge  │  │
│  │ Channel  │  │ Channel  │  │  (IoT)        │  │
│  └────┬─────┘  └────┬─────┘  └───────┬───────┘  │
│       └──────────────┼────────────────┘          │
│                      ▼                           │
│           ┌──────────────────┐                   │
│           │  Agent Gateway   │                   │
│           └────────┬─────────┘                   │
│                    ▼                             │
│           ┌──────────────────┐                   │
│           │  Agent Pipeline  │                   │
│           │  (Middleware)    │                   │
│           └────────┬─────────┘                   │
│                    ▼                             │
│     ┌──────────────┼──────────────┐              │
│     ▼              ▼              ▼              │
│ ┌────────┐  ┌───────────┐  ┌──────────┐         │
│ │  LLM   │  │   Tools   │  │  Skills  │         │
│ │Invoker │  │  Plugins  │  │  Store   │         │
│ └────────┘  └───────────┘  └──────────┘         │
│                    │                             │
│     ┌──────────────┼──────────────┐              │
│     ▼              ▼              ▼              │
│ ┌────────┐  ┌───────────┐  ┌──────────┐         │
│ │  NATS  │  │ PostgreSQL│  │  Device  │         │
│ │Messaging│ │  Storage  │  │  Shadow  │         │
│ └────────┘  └───────────┘  └──────────┘         │
└─────────────────────┬───────────────────────────┘
                      │ NATS Leaf / MQTT
        ┌─────────────┼─────────────┐
        ▼             ▼             ▼
   ┌─────────┐  ┌─────────┐  ┌─────────┐
   │  Edge   │  │  Edge   │  │  Edge   │
   │  Agent  │  │  Agent  │  │  Agent  │
   └─────────┘  └─────────┘  └─────────┘
```

---

## 5. 核心模組與功能需求

### 5.1 Channel Adapter（通訊層）

#### 已實現
* Web UI (SSE streaming)
* Telegram Bot (polling mode)

#### 擴充目標
* Line Messaging API
* Discord
* MQTT (IoT 裝置通訊)
* CoAP (低功耗 IoT 協議)

---

### 5.2 Agent Pipeline（Middleware）

#### 設計

```csharp
IAgentPipeline
  .Use<ErrorHandling>()
  .Use<SecretRedaction>()
  .Use<Logging>()
  .Use<Timeout>()
  .Use<LLMInvoker>()
  .Use<ToolExecutor>();
```

#### 已實現 Middleware
* Logging / Error Handling / Timeout / Secret Redaction

---

### 5.3 LLM Abstraction Layer

#### 已實現
* OpenAI / Azure OpenAI
* Ollama (本地 LLM)
* Two-layer Provider（Global + Per-user）

#### 擴充
* Anthropic Claude
* Custom OpenAI-compatible endpoint

---

### 5.4 Tool Plugin System（裝置驅動層）

#### 機制
* C# DLL 自動掃描載入（AssemblyLoadContext）
* `AgentToolBase<TArgs>` 基類 + Record 參數自動生成 JSON Schema
* Per-user workspace 隔離

#### 已實現 Tools
* FileSystem / Shell / Git / GitHub / AzureDevOps
* Http / WebSearch (SearXNG) / Notion / PDF / ImageGen
* Tmux / Preference

#### IoT 擴充目標
* DeviceManagement（裝置 CRUD、狀態查詢）
* FirmwareDeployment（OTA 推送）
* TelemetryQuery（感測器數據查詢）

---

### 5.5 Skill System（應用層）

#### 機制
* Markdown-based 宣告式定義（SKILL.md）
* 組合 Tool + LLM 指令
* `@skill-name` mention 觸發 / CronJob context 引用

#### 已實現 Skills
* daily-ado-report / ado-task-sync / manage-cron-jobs

---

### 5.6 CronJob Scheduler（排程系統）

#### 已實現
* 分散式排程 (NATS JetStream + Leader Election)
* 支援 cron / daily / weekly / monthly
* 手動 / 排程觸發
* 執行紀錄與歷史

---

### 5.7 Security（安全層）

#### 已實現
* JWT Authentication + Refresh Token
* RBAC (User / Admin / SuperAdmin)
* AES-256 加密設定儲存
* Per-user data isolation (EF Core Query Filter)
* Audit Logging
* Login Rate Limiting / Path Traversal Protection
* Content Security Policy (CSP)

---

## 6. IoT / Edge 擴充藍圖

### 6.1 Device Domain Model

```csharp
// 新增 Domain 實體
Device          // id, name, type, status, firmwareVersion, lastSeen, tags
DeviceGroup     // fleet 管理，批量操作
DeviceShadow    // desired state vs reported state
DeviceEvent     // telemetry, alert, heartbeat
```

### 6.2 MQTT Channel Adapter

```
實作 IChannelAdapter + IHostedService
- devices/{id}/telemetry   → 上報感測器數據
- devices/{id}/command     → 下發控制指令
- devices/{id}/shadow      → 狀態同步
- MQTT v5 topic routing
```

### 6.3 Edge Agent（ClawOS.Edge）

```
輕量版 ClawOS，部署在 Edge Device：
- .NET 8 AOT 發布（小 binary）
- 本地 Tool 執行（Shell, FileSystem）
- NATS Leaf Node 或 MQTT client 連回 Cloud
- 離線排隊、重連後同步
- Provision key 自動註冊
```

### 6.4 Fleet Management

```
Device Registration Flow:
1. Edge Agent 首次啟動 → provision key 註冊
2. Cloud 回傳 device certificate + config
3. NATS subject hierarchy 實現群組廣播：
   - devices.>              （所有裝置）
   - devices.fleet-A.>      （特定 fleet）
   - devices.{deviceId}.cmd （單一裝置）
```

### 6.5 OTA Firmware Update

```
利用 CronJob + Skill 系統：
- firmware-rollout skill
- Canary deployment（10% → 觀察 → 全量）
- NATS JetStream at-least-once delivery
- DeviceShadow 追蹤 desired vs actual version
```

---

## 7. 非功能性需求（NFR）

| 類型 | 需求 |
|------|------|
| 穩定性 | 可長時間運行（24/7 Agent） |
| 可觀測性 | OpenTelemetry + Grafana Dashboard |
| 擴展性 | Plugin 不影響核心；水平擴展 via NATS |
| 可測試性 | 核心可單元測試；Integration Test |
| 安全性 | 加密儲存、Workspace 隔離、Audit Trail |
| 可部署性 | Docker Compose / Kubernetes / Edge AOT |

---

## 8. 技術選型

| 層級 | 技術 |
|------|------|
| Runtime | .NET 10 |
| Web | ASP.NET Core |
| Database | PostgreSQL (EF Core) |
| Messaging | NATS JetStream (Broker + Bus) |
| Search | SearXNG |
| Observability | OpenTelemetry + Prometheus + Grafana |
| Container | Docker / Docker Compose |
| IoT Protocol | MQTT v5 / CoAP (planned) |
| Edge Runtime | .NET 8 AOT (planned) |
| Framework | Weda.Core (DDD, CQRS, SAGA) |

---

## 9. 開發里程碑

### Phase 1：Agent Core (已完成)
* CLI + Web UI
* LLM Tool Calling + Streaming
* 內建 Tools + Markdown Skills
* Multi-user + RBAC

### Phase 2：Platform Features (已完成)
* Middleware Pipeline
* CronJob Scheduler (NATS distributed)
* Telegram Channel
* Audit Logging + Security Hardening

### Phase 3：Enterprise & IoT (進行中)
* Device Domain Model + MQTT Channel
* Edge Agent (AOT)
* Fleet Management + OTA
* Agent-to-Agent Communication

### Phase 4：Ecosystem
* Skill Marketplace
* Plugin SDK for 3rd-party Tools
* Multi-cluster Federation
* Edge AI Inference

---

## 10. 成功指標（Success Metrics）

* 可透過自然語言管理 100+ 台 edge device
* 新增 Tool / Channel 不需改核心程式
* Agent 可連續運行 > 30 天
* Cloud → Edge 延遲 < 500ms (NATS Leaf Node)
* OTA 推送成功率 > 99.5%

---

## 11. User Stories

### US-01：多 Channel 對話整合
**作為**使用者，**我希望**透過 Telegram 或 Line 與 ClawOS 進行對話，**以便**不需開啟電腦也能操作 Agent。

**Acceptance Criteria:**
- 支援 Telegram Bot Channel
- 支援 Line Messaging API Channel
- 訊息雙向同步，支援文字與圖片

---

### US-02：對話式 Cron Job 管理
**作為**使用者，**我希望**透過對話方式新增 Cron Jobs，**以便**不需手動編輯設定檔即可排程任務。

**Acceptance Criteria:**
- 自然語言描述排程（如「每天早上 9 點執行 X」）
- 支援 list/add/remove/update cron jobs
- 持久化儲存 cron 設定

---

### US-03：Web 管理介面
**作為**管理員，**我希望**有一個網頁介面來管理 Config、Cron Jobs、API Keys、Models、Skills，**以便**集中管理所有設定與功能。

**Acceptance Criteria:**
- Web Dashboard：Config / User / Model Provider 管理
- Cron Jobs CRUD 介面
- Audit Log 查看器
- 內建 Web Chat 介面

---

### US-04：24/7 自主開發模式
**作為**開發者，**我希望**Agent 可以自主、不停歇地進行程式開發，**以便**持續推進專案進度。

**Acceptance Criteria:**
- Agent 具備 Task Queue 與優先序管理
- 自動 commit、push、建立 PR
- 錯誤自動恢復與重試機制
- 進度報告與通知（Telegram/Line）

---

### US-05：Azure DevOps 整合與自動化
**作為**開發者，**我希望**Agent 可讀取 Azure DevOps 的 Work Items 並自動更新狀態，**以便**減少手動維護 ADO 的時間。

**Acceptance Criteria:**
- ADO REST API 整合（Work Items CRUD）
- 根據 Git commits 自動推斷 Task 完成度
- 雙向同步：本地 todo ↔ ADO Work Items

---

### US-06：IoT 裝置管理
**作為**平台管理員，**我希望**透過自然語言管理 edge device（查詢狀態、推送韌體、重啟裝置），**以便**不需登入每台裝置即可批量管理。

**Acceptance Criteria:**
- Device 註冊與 Fleet 分組
- Device Shadow（desired vs reported state）
- 自然語言指令：「重啟所有 fleet-A 中超過 24 小時沒回報的裝置」
- OTA 韌體推送（canary → full rollout）

---

### US-07：Edge Agent 部署
**作為**IoT 工程師，**我希望**在 edge device 上部署輕量版 ClawOS，**以便**裝置可以本地執行 AI 任務並與 Cloud 同步。

**Acceptance Criteria:**
- .NET AOT 發布（< 50MB binary）
- NATS Leaf Node 或 MQTT 連回 Cloud
- 離線模式：排隊 → 重連後同步
- Provision key 自動註冊

---

## 12. 開放問題

* Plugin 安全邊界策略（Sandbox vs Trust）
* Edge Agent 的最低硬體需求
* MQTT vs NATS Leaf Node 的選型考量
* Device Shadow 一致性保證策略
* OTA 失敗回滾機制
* Multi-cluster Federation 架構
* Edge-side LLM inference（小模型本地推論）

---

**本 PRD 為工程導向文件，可直接作為實作藍本。**
