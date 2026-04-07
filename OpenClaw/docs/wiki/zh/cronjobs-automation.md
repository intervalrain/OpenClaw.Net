---
sidebar_position: 7
sidebar_label: 'CronJob 與自動化'
hide_title: true
title: 'CronJob 與自動化'
keywords: ['OpenClaw', 'CronJob', '自動化', '排程器', '工具實例']
description: '透過 CronJob 與工具實例排程和自動化 AI Agent 任務'
---

# CronJob 與自動化

> 排程與自動化 AI Agent 任務

## 概述

OpenClaw 提供兩個自動化基礎元件：

- **CronJob** — 排程或手動觸發的 LLM 驅動任務
- **工具實例** — 預設參數的工具，可重複使用

## CronJob

### 建立 CronJob

透過 Web UI 的 **CronJobs** 頁面或 API 建立 CronJob。每個 CronJob 定義：

| 欄位 | 說明 |
|------|------|
| Name | 工作的顯示名稱 |
| Content | 給 LLM 的提示/指令 |
| Schedule | Cron 表達式（如 `0 9 * * 1-5` 代表平日 9 AM） |
| Wake Mode | `Scheduled`、`Manual` 或 `Both` |
| Context | 選填的 JSON 上下文（技能綁定、變數） |
| Session ID | 選填的對話 Session，用於延續對話 |

### 喚醒模式

| 模式 | 說明 |
|------|------|
| **Scheduled** | 依 Cron 排程自動執行 |
| **Manual** | 僅在明確觸發時執行 |
| **Both** | 可手動觸發，同時也依排程執行 |

### 執行生命週期

```
CronJob
  │
  ├── 觸發（排程或手動）
  │
  ▼
CronJobExecution
  ├── 狀態: Pending
  ├── 狀態: Running    ── Agent Pipeline 執行
  ├── 狀態: Completed  ── 輸出 + 工具呼叫已儲存
  └── 狀態: Failed     ── 錯誤訊息已儲存
      或 Cancelled
```

每次執行建立一個 `CronJobExecution` 記錄，包含：
- 觸發類型（Manual / Scheduled）
- 完整輸出文字
- 工具呼叫 JSON
- 錯誤訊息（如果失敗）
- 開始與完成時間戳

### 技能綁定

CronJob 可在上下文中引用 Markdown 技能。執行時，技能的指令與工具清單會注入 Agent Pipeline：

```json
{
  "skill": "daily-ado-report"
}
```

## 工具實例

工具實例是預設參數的工具，具備使用者定義的顯示名稱。

### 使用情境

- 儲存常用的工具設定
- 建立可重複使用的「動作」，無需寫程式碼
- 在 CronJob 中引用，確保一致的執行

### 範例

名為「檢查生產 API」的工具實例可能預設：
- 工具：`http_request`
- 引數：`{ "url": "https://api.example.com/health", "method": "GET" }`

## Agent 活動追蹤

所有 CronJob 執行與聊天互動都記錄在 **Agent Activity** 系統中：

| 欄位 | 說明 |
|------|------|
| Type | `Chat`、`CronJob` 或 `ToolExecution` |
| Status | `Started`、`Thinking`、`ToolExecuting`、`Completed`、`Failed` |
| SourceId | 來源對話或 CronJob 的參考 |
| Detail | 活動特定的詳細資訊 |

活動可在 **Agents** 頁面檢視，用於監控與除錯。

## API 端點

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/cron-job` | 列出 CronJob |
| `POST` | `/api/v1/cron-job` | 建立 CronJob |
| `PUT` | `/api/v1/cron-job/{id}` | 更新 CronJob |
| `DELETE` | `/api/v1/cron-job/{id}` | 刪除 CronJob |
| `POST` | `/api/v1/cron-job/{id}/execute` | 手動執行 |
| `GET` | `/api/v1/cron-job/{id}/executions` | 列出執行記錄 |
| `GET/POST/PUT/DELETE` | `/api/v1/tool-instance` | 工具實例 CRUD |

## 相關資源

- [Markdown 技能指南](./markdown-skills-guide.md) — CronJob 上下文中使用的技能
- [API 參考](./api-reference.md) — 完整 API 文件
