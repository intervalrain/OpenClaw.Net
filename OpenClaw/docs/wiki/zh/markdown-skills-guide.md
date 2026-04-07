---
sidebar_position: 5
sidebar_label: 'Markdown 技能'
hide_title: true
title: 'Markdown 技能指南'
keywords: ['OpenClaw', '技能', 'SKILL.md', 'Markdown', '宣告式']
description: '使用 SKILL.md 檔案建立宣告式技能，將工具與 LLM 指令組合'
---

# Markdown 技能指南

> 將工具與 LLM 指令組合的宣告式技能

## 概述

Markdown 技能是以 `SKILL.md` 檔案撰寫的宣告式定義。允許您將現有的 C# 工具與自訂 LLM 指令組合 — 無需寫程式碼。技能可透過聊天中的 `@skill-name` 提及來呼叫，或在 CronJob 上下文中引用。

## SKILL.md 格式

在 `skills/` 目錄中建立 `SKILL.md` 檔案：

```
skills/
└── my-skill/
    └── SKILL.md
```

### 結構

```markdown
---
name: my-skill
description: 這個技能做什麼。當使用者詢問 X 時使用。
tools: [tool_name_1, tool_name_2]
---

## Instructions

You are a helpful assistant that...

### Steps

1. 首先，做 X
2. 然後，做 Y
3. 最後，做 Z

### Output Format

在此描述預期的輸出格式。

### Rules
- 規則 1
- 規則 2
```

### Frontmatter 欄位

| 欄位 | 型別 | 必要 | 說明 |
|------|------|------|------|
| `name` | string | 是 | 唯一技能識別碼（用於 `@skill-name` 提及） |
| `description` | string | 是 | 技能功能說明與使用時機 |
| `tools` | string[] | 是 | 此技能可使用的 C# 工具名稱清單 |

## 範例：每日 ADO 報告

```markdown
---
name: daily-ado-report
description: Generate a daily work item summary report from Azure DevOps.
  Use when user asks for daily report, sprint status, or work item summary.
tools: [azure_devops, write_file]
---

## Instructions

You are a project management assistant generating a daily work item report.

### Steps

1. Call `azure_devops` with operation `my_work_items` to get current sprint items
2. Analyze the work items and group them by status
3. Generate a concise summary in markdown format

### Output Format

# Daily Report - {date}

## Summary
- Total items: {count}
- Active: {count}

## Active Work Items
| ID | Title | Remaining Work |
|----|-------|---------------|

### Rules
- Keep the report concise
- Highlight items with 0 remaining work that are still Active
- Flag any items that seem stale
```

## 呼叫技能

### 在聊天中

以 `@` 前綴提及技能名稱：

```
@daily-ado-report 產生今天的報告
```

### 在 CronJob 中

在 CronJob 的上下文中引用技能，使排程執行時使用技能的指令與工具清單。

## 技能設定

技能可依工作區啟用或停用。停用的技能不會出現在該工作區 Agent 的工具清單中。

## 最佳實踐

1. **清楚的描述** — 包含觸發詞（「Use when user asks for...」），讓 LLM 知道何時啟動技能
2. **精確的工具** — 只列出技能實際需要的工具
3. **結構化步驟** — 將指令拆分為編號步驟，確保一致的執行
4. **輸出格式** — 定義預期的輸出格式，使結果一致
5. **規則區塊** — 加入限制條件與邊界情況處理

## 相關資源

- [工具開發指南](./tool-development-guide.md) — 建立技能所組合的 C# 工具
- [CronJob 與自動化](./cronjobs-automation.md) — 在排程任務中使用技能
