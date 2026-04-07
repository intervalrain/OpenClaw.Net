---
sidebar_position: 6
sidebar_label: '模型供應商'
hide_title: true
title: '模型供應商'
keywords: ['OpenClaw', 'LLM', '模型供應商', 'OpenAI', 'Ollama']
description: '雙層模型供應商架構，用於管理 LLM 連線'
---

# 模型供應商

> 雙層架構管理 LLM 連線

## 概述

OpenClaw 使用雙層模型供應商系統，將全域管理與使用者個人設定分離：

- **全域供應商** — 由 SuperAdmin 管理，所有使用者共享
- **使用者供應商** — 使用者以自己的 API Key 新增的個人供應商

## 供應商解析順序

當使用者發送聊天訊息時，系統依以下順序解析 LLM 供應商：

```
1. 使用者的預設供應商（UserModelProvider，IsDefault=true）
2. 使用者的第一個可用供應商
3. 連結至使用者的全域供應商
4. 備援至 Ollama（localhost）
```

## 全域供應商（SuperAdmin）

SuperAdmin 透過 **Settings > Models** 設定全域供應商：

| 欄位 | 說明 |
|------|------|
| Name | 顯示名稱（如「GPT-4o」、「Claude Sonnet」） |
| Type | 供應商類型（`openai`、`ollama` 或自訂） |
| URL | API 端點 URL |
| Model Name | 模型識別碼（如 `gpt-4o`、`llama3.1`） |
| API Key | 以 AES-256 靜態加密 |
| Max Context Tokens | 最大上下文視窗大小 |
| Allow User Override | 是否允許使用者基於此建立自己的供應商 |

### 支援的供應商類型

| 類型 | 說明 | 範例 URL |
|------|------|----------|
| `openai` | OpenAI API 與相容端點 | `https://api.openai.com/v1` |
| `ollama` | 本機 Ollama 伺服器 | `http://localhost:11434` |
| 自訂 | 任何 OpenAI 相容 API | `https://api.groq.com/openai/v1` |

## 使用者供應商

使用者可透過 **Settings > Models** 新增自己的供應商：

- **連結全域** — 使用全域供應商，並以個人 API Key 覆寫
- **新增自訂** — 設定完全自訂的供應商端點

每位使用者可設定一個供應商為**預設**，用於聊天會話。

## 透過環境變數設定

Docker 部署時，可透過環境變數設定預設供應商：

| 變數 | 說明 |
|------|------|
| `LLM_PROVIDER` | 預設供應商類型（`ollama` 或 `openai`） |
| `OPENAI_API_KEY` | OpenAI API Key |
| `OPENAI_MODEL` | OpenAI 模型名稱 |
| `OLLAMA_URL` | Ollama 伺服器 URL |
| `OLLAMA_MODEL` | Ollama 模型名稱 |

## 安全性

- API Key 以 AES-256 靜態加密
- API 回應中永不回傳金鑰（僅遮罩顯示）
- 使用者金鑰互相隔離 — 使用者無法看到彼此的憑證

## 相關資源

- [快速開始](./getting-started.md) — 初始供應商設定
- [安全性](./security.md) — 加密細節
