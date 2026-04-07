---
sidebar_position: 3
sidebar_label: '架構'
hide_title: true
title: '架構指南'
keywords: ['OpenClaw', '架構', 'Clean Architecture', 'DDD', 'CQRS']
description: 'OpenClaw.NET 的系統架構、設計模式與領域模型'
---

# 架構指南

> Clean Architecture、CQRS 與領域驅動設計

## 設計原則

OpenClaw.NET 遵循以下架構模式：

- **Clean Architecture** — 依賴方向朝內：Domain 沒有外部依賴
- **領域驅動設計 (DDD)** — 豐富的領域實體與聚合根
- **CQRS** — 命令與查詢透過 Mediator 模式分離
- **多使用者隔離** — EF Core 全域查詢篩選器確保資料依使用者劃分

## 層級概觀

```
┌────────────────────────────────────────────────────────────────┐
│                         展示層                                 │
│   Controllers (36)  ·  SSE 串流  ·  Swagger  ·  Wiki          │
├────────────────────────────────────────────────────────────────┤
│                        應用程式層                               │
│   Commands / Queries / Handlers  ·  Agent Pipeline             │
│   Tool Registry  ·  Skill Settings  ·  Context Compressor     │
├────────────────────────────────────────────────────────────────┤
│                         領域層                                  │
│   實體 (22)  ·  聚合根  ·  值物件                               │
│   Repository 介面  ·  領域事件                                  │
├────────────────────────────────────────────────────────────────┤
│                       基礎設施層                                │
│   EF Core  ·  Repositories  ·  JWT 認證  ·  AES 加密           │
│   NATS 訊息佇列  ·  Email  ·  稽核日誌                         │
├────────────────────────────────────────────────────────────────┤
│                         工具層                                  │
│   FileSystem · Git · GitHub · AzureDevOps · Shell · Http      │
│   WebSearch · Pdf · ImageGen · Notion · Tmux · Preference     │
└────────────────────────────────────────────────────────────────┘
```

## 領域模型

### 認證與使用者

| 實體 | 用途 |
|------|------|
| **User** | 核心使用者帳號，含 Email、角色、權限、工作區路徑 |
| **UserPreference** | 鍵值對使用者偏好設定 |
| **UserConfig** | 加密的使用者機密資料（API Key、Token） |
| **EmailVerification** | Email 驗證流程 |
| **PasswordResetToken** | 密碼重設流程 |

### 聊天與對話

| 實體 | 用途 |
|------|------|
| **Conversation** | 聊天會話（聚合根），含訊息歷史 |
| **ConversationMessage** | 個別訊息，含角色（User/Assistant）與內容 |

### Agent 執行

| 實體 | 用途 |
|------|------|
| **CronJob** | 排程/手動觸發的工作，含 Cron 表達式、上下文與技能綁定 |
| **CronJobExecution** | 單次執行記錄，含狀態、輸出與工具呼叫 |
| **ToolInstance** | 預設參數的工具，具備顯示名稱 |
| **AgentActivity** | 僅附加的活動日誌（Chat/CronJob/ToolExecution） |

### 設定

| 實體 | 用途 |
|------|------|
| **ModelProvider** | 全域 LLM 供應商（SuperAdmin 管理） |
| **UserModelProvider** | 每使用者 LLM 供應商（自有或連結全域） |
| **AppConfig** | 應用程式級設定 |
| **ChannelSettings** | 每使用者頻道設定（如 Telegram） |

### 工作區與協作

| 實體 | 用途 |
|------|------|
| **Workspace** | 團隊或個人工作區 |
| **WorkspaceMember** | 成員資格，含角色（檢視者/成員/擁有者） |
| **DirectoryPermission** | 每使用者的檔案系統路徑可見性 |
| **SkillSetting** | 每工作區的技能啟用/停用切換 |

### 整合與稽核

| 實體 | 用途 |
|------|------|
| **ChannelUserBinding** | 外部平台帳號對應 |
| **Notification** | 使用者通知 |
| **AuditLog** | 安全稽核軌跡（僅 SuperAdmin） |

## Agent Pipeline

Agent Pipeline 是 LLM 互動的核心執行引擎：

```
使用者訊息
    │
    ▼
┌──────────────┐
│  斜線命令     │ ── @skill-name 提及解析
│   解析器      │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│   上下文     │ ── 對話歷史 + 技能指令
│   組裝器     │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ LLM 供應商   │ ── Ollama / OpenAI / 自訂
│   工廠       │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  工具呼叫    │ ── Tool Registry 解析並執行
│   執行器     │    LLM 回應中的工具呼叫
└──────┬───────┘
       │
       ▼
┌──────────────┐
│   上下文     │ ── 接近 Token 上限時
│   壓縮器     │    壓縮歷史記錄
└──────┬───────┘
       │
       ▼
  SSE 回應串流
```

## 關鍵應用程式服務

| 服務 | 職責 |
|------|------|
| `IAgentPipeline` | 主要 LLM 執行引擎 |
| `IToolRegistry` | 工具/技能探索與呼叫 |
| `ILlmProviderFactory` | 供應商解析（使用者 > 全域 > 備援） |
| `IContextCompressor` | Token 用量最佳化 |
| `ISlashCommandParser` | 技能提及解析（`@skill-name`） |
| `CronJobSchedulerService` | 背景排程器，具備領導者選舉 |

## 資料隔離

多使用者資料隔離在資料庫層級執行：

1. 所有使用者範圍的實體實作 `IUserScoped` 介面
2. EF Core 全域查詢篩選器自動加入 `WHERE UserId = @currentUserId`
3. SuperAdmin 可繞過篩選器進行管理操作
4. 工作區檔案系統路徑依使用者隔離，具備路徑遍歷防護

## 相關資源

- [工具開發指南](./tool-development-guide.md)
- [安全性](./security.md)
- [API 參考](./api-reference.md)
