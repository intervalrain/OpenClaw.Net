---
sidebar_position: 9
sidebar_label: 'API 參考'
hide_title: true
title: 'API 參考'
keywords: ['OpenClaw', 'API', 'REST', '端點', 'Swagger']
description: 'OpenClaw.NET REST API 端點參考'
---

# API 參考

> REST API 端點（v1.0）

伺服器執行時，可在 `/swagger` 取得完整的互動式文件。

## 認證

| 方法 | 路徑 | 說明 |
|------|------|------|
| `POST` | `/api/v1/auth/login` | 以 Email 和密碼登入 |
| `POST` | `/api/v1/auth/register` | 註冊新帳號 |
| `POST` | `/api/v1/auth/refresh` | 更新 Access Token |
| `POST` | `/api/v1/auth/logout` | 登出並使 Refresh Token 失效 |

其他所有端點需要在 `Authorization: Bearer <token>` 標頭中提供有效的 JWT Token。

## 聊天與對話

| 方法 | 路徑 | 說明 |
|------|------|------|
| `POST` | `/api/v1/chat/stream` | 串流聊天回應（SSE） |
| `GET` | `/api/v1/conversation` | 列出對話 |
| `POST` | `/api/v1/conversation` | 建立對話 |
| `GET` | `/api/v1/conversation/{id}` | 取得對話與訊息 |
| `DELETE` | `/api/v1/conversation/{id}` | 刪除對話 |

## 模型供應商

### 全域供應商（SuperAdmin）

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/model-provider` | 列出全域供應商 |
| `POST` | `/api/v1/model-provider` | 建立全域供應商 |
| `PUT` | `/api/v1/model-provider/{id}` | 更新全域供應商 |
| `DELETE` | `/api/v1/model-provider/{id}` | 刪除全域供應商 |

### 使用者供應商

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/user-model-provider` | 列出使用者的供應商 |
| `POST` | `/api/v1/user-model-provider` | 新增使用者供應商 |
| `PUT` | `/api/v1/user-model-provider/{id}` | 更新使用者供應商 |
| `DELETE` | `/api/v1/user-model-provider/{id}` | 刪除使用者供應商 |
| `GET` | `/api/v1/user-model-provider/available` | 列出可用的全域供應商 |

## CronJob

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/cron-job` | 列出 CronJob |
| `POST` | `/api/v1/cron-job` | 建立 CronJob |
| `PUT` | `/api/v1/cron-job/{id}` | 更新 CronJob |
| `DELETE` | `/api/v1/cron-job/{id}` | 刪除 CronJob |
| `POST` | `/api/v1/cron-job/{id}/execute` | 手動觸發執行 |
| `GET` | `/api/v1/cron-job/{id}/executions` | 列出執行歷史 |

## 工具實例

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/tool-instance` | 列出工具實例 |
| `POST` | `/api/v1/tool-instance` | 建立工具實例 |
| `PUT` | `/api/v1/tool-instance/{id}` | 更新工具實例 |
| `DELETE` | `/api/v1/tool-instance/{id}` | 刪除工具實例 |

## 設定

### 使用者設定（加密）

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/user-config` | 列出使用者設定鍵 |
| `GET` | `/api/v1/user-config/{key}` | 取得設定值 |
| `PUT` | `/api/v1/user-config/{key}` | 設定值 |
| `DELETE` | `/api/v1/user-config/{key}` | 刪除設定項目 |

### 應用程式設定（SuperAdmin）

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/app-config` | 列出應用程式設定鍵 |
| `PUT` | `/api/v1/app-config/{key}` | 設定應用程式設定值 |
| `DELETE` | `/api/v1/app-config/{key}` | 刪除應用程式設定項目 |

### 頻道設定

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/channel-settings/telegram` | 取得 Telegram 設定 |
| `PUT` | `/api/v1/channel-settings/telegram` | 更新 Telegram 設定 |

## 使用者管理（SuperAdmin）

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/user-management` | 列出所有使用者 |
| `POST` | `/api/v1/user-management/{id}/approve` | 核准註冊 |
| `POST` | `/api/v1/user-management/{id}/ban` | 封禁使用者 |
| `POST` | `/api/v1/user-management/{id}/unban` | 解除封禁 |
| `PUT` | `/api/v1/user-management/{id}/role` | 更新使用者角色 |

## 工作區

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/workspace` | 列出工作區 |
| `POST` | `/api/v1/workspace` | 建立工作區 |
| `GET` | `/api/v1/workspace/{id}` | 取得工作區詳情 |
| `PUT` | `/api/v1/workspace/{id}` | 更新工作區 |
| `GET` | `/api/v1/workspace/{id}/files` | 列出工作區檔案 |

## Agent 與活動

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/agents` | 列出 Agent |
| `GET` | `/api/v1/agent-activity` | 列出 Agent 活動 |
| `GET` | `/api/v1/agent-activity/stream` | 串流活動（SSE） |

## 稽核日誌（SuperAdmin）

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/audit-log` | 查詢稽核日誌（支援篩選） |

## 系統設定

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/api/v1/setup/status` | 檢查系統是否已初始化 |
| `POST` | `/api/v1/setup/initialize` | 建立初始 SuperAdmin 帳號 |

## 相關資源

- [快速開始](./getting-started.md) — 設定與存取
- [安全性](./security.md) — 認證細節
