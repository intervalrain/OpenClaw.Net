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

### 3. 技術架構改進

- ✅ Clean Architecture 設計（Domain → Application → Infrastructure → API）
- ✅ Skills 完全解耦，透過 `IAgentSkill` 介面統一管理
- ✅ 動態參數驗證（`ToolParameters` with JSON Schema）
- ✅ SSE (Server-Sent Events) 串流輸出
- ✅ Docker Compose 完整基礎設施（PostgreSQL, NATS, SearXNG）

---

## 待開發 Skills 清單

參考 [OpenClaw Skills](https://github.com/cased/openclaw/tree/main/skills)，以下是建議優先開發的 skills：

### 高優先級（按優先順序排列）

| Priority | Skill | 描述 | 需求 | 預估工時 |
|----------|-------|------|------|----------|
| 🔴 P0-1 | **Weather** | 查詢天氣和預報（wttr.in） | `curl`（已內建） | 2-3h |
| 🔴 P0-2 | **GitHub** | GitHub 操作（issues, PRs, CI） | `gh` CLI | 4-6h |
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

## 技術債務和改進項目

### 需要修復
- ⚠️ 無明顯技術債務，目前架構健康

### 可選優化
- 🔧 加入 Skill 版本管理
- 🔧 Skill 執行統計和監控（已有 Grafana 基礎）
- 🔧 Skill 權限系統（限制某些 skills 只能特定用戶使用）
- 🔧 Skill 參數驗證增強（更詳細的錯誤訊息）
- 🔧 Skill 測試覆蓋率提升

---

## 下一步行動

### 立即可做
1. ✅ **優先級已確認**: Weather → GitHub → Git → Azure DevOps → Image Gen → PDF → Tmux → Notion
2. **開始實作 Weather Skill** (P0-1)
   - 使用 `wttr.in` API（無需 API key）
   - 參考 `/Users/rainhu/workspace/openclaw/skills/weather/SKILL.md`
   - 預估工時: 2-3 小時
3. 建立 skill 開發範本和最佳實踐文檔

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

**更新時間**: 2026-02-24
**狀態**: Skills 系統核心功能完成，準備擴充 skill 生態系統
