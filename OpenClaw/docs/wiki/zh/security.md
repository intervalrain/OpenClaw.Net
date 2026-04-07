---
sidebar_position: 8
sidebar_label: '安全性'
hide_title: true
title: '安全性'
keywords: ['OpenClaw', '安全', 'JWT', '加密', 'RBAC', '稽核']
description: 'OpenClaw.NET 的認證、授權、加密與稽核日誌'
---

# 安全性

> 認證、授權、加密與稽核日誌

## 認證

### JWT Token

OpenClaw 使用 JWT（JSON Web Token）進行認證：

- 具備可設定過期時間的 Access Token
- Refresh Token 輪替 — 每次更新都發行新的 Token 對
- 登入失敗後帳號鎖定

### 註冊流程

1. 使用者以 Email 和密碼註冊
2. SuperAdmin 核准註冊（或設定為自動核准）
3. 核准後使用者即可存取

## 授權

### 角色存取控制（RBAC）

三層角色系統：

| 角色 | 能力 |
|------|------|
| **User** | 聊天、管理自己的對話、CronJob、工具與供應商 |
| **Admin** | 所有 User 能力 + 部分管理功能 |
| **SuperAdmin** | 完整系統存取：使用者管理、全域供應商、應用程式設定、稽核日誌 |

### 權限系統

超越角色的細粒度權限：

| 權限 | 說明 |
|------|------|
| `OpenClaw` | 平台基本存取權 |
| 自訂權限 | 依功能可擴充 |

### 封禁系統

- SuperAdmin 可附帶原因封禁使用者
- `BanCheckMiddleware` 在每個請求上執行封禁檢查
- 封禁狀態快取在記憶體中以提升效能
- 單一 SuperAdmin 保護 — 最後一位 SuperAdmin 無法被封禁

## 加密

### 靜態加密

所有敏感資料以 AES-256 加密：

- 使用者 API Key（`UserConfig`）
- 全域供應商 API Key（`ModelProvider`）
- 頻道設定（`ChannelSettings`）
- 應用程式級機密（`AppConfig`）

加密金鑰透過 `OPENCLAW_ENCRYPTION_KEY` 環境變數設定。

### 密碼雜湊

密碼使用業界標準演算法（bcrypt/PBKDF2）雜湊處理，永不以明文儲存。

## 內容安全政策（CSP）

所有頁面嚴格執行 CSP：

- 禁止內嵌腳本與事件處理器
- 所有 JavaScript 從外部檔案載入
- 樣式來源限制為同源與受信任的 CDN
- Frame ancestors 受限

## 資料隔離

### 使用者級隔離

- 所有使用者範圍的實體實作 `IUserScoped`
- EF Core 全域查詢篩選器執行 `WHERE UserId = @currentUserId`
- SuperAdmin 可繞過篩選器進行管理操作

### 檔案系統隔離

- 每位使用者的工作區是獨立的目錄
- 路徑遍歷防護阻止存取工作區外的檔案
- `DirectoryPermission` 實體控制每路徑的可見性

## 稽核日誌

### 記錄內容

`AuditLoggingMiddleware` 擷取：

| 欄位 | 說明 |
|------|------|
| UserId / UserEmail | 執行動作的使用者 |
| Action | 執行了什麼 |
| HttpMethod / Path | 呼叫的 API 端點 |
| StatusCode | 回應狀態碼 |
| IpAddress | 用戶端 IP |
| UserAgent | 用戶端 User Agent |
| Timestamp | 發生時間 |

### 存取

- 稽核日誌**僅 SuperAdmin** 可存取
- 可透過管理介面稽核日誌檢視器查看
- 可透過 API 查詢，支援篩選與分頁

### 保留政策

可設定保留政策，自動清理舊的記錄。

## 速率限制

- 登入端點：速率限制以防止暴力破解
- 註冊端點：速率限制以防止濫用

## 相關資源

- [架構指南](./architecture.md) — 系統設計
- [模型供應商](./model-providers.md) — API Key 加密
