---
sidebar_position: 6
sidebar_label: 'Model Providers'
hide_title: true
title: 'Model Providers'
keywords: ['OpenClaw', 'LLM', 'Model Provider', 'OpenAI', 'Ollama']
description: 'Two-layer model provider architecture for managing LLM connections'
---

# Model Providers

> Two-layer architecture for managing LLM connections

## Overview

OpenClaw uses a two-layer model provider system that separates global administration from per-user configuration:

- **Global Providers** — Managed by SuperAdmin, shared across all users
- **User Providers** — Personal providers added by individual users with their own API keys

## Provider Resolution Order

When a user sends a chat message, the system resolves the LLM provider in this order:

```
1. User's default provider (UserModelProvider with IsDefault=true)
2. User's first available provider
3. Global provider linked to user
4. Fallback to Ollama (localhost)
```

## Global Providers (SuperAdmin)

SuperAdmins configure global providers via **Settings > Models**:

| Field | Description |
|-------|-------------|
| Name | Display name (e.g., "GPT-4o", "Claude Sonnet") |
| Type | Provider type (`openai`, `ollama`, or custom) |
| URL | API endpoint URL |
| Model Name | Model identifier (e.g., `gpt-4o`, `llama3.1`) |
| API Key | Encrypted at rest with AES-256 |
| Max Context Tokens | Maximum context window size |
| Allow User Override | Whether users can create their own providers based on this |

### Supported Provider Types

| Type | Description | Example URL |
|------|-------------|-------------|
| `openai` | OpenAI API and compatible endpoints | `https://api.openai.com/v1` |
| `ollama` | Local Ollama server | `http://localhost:11434` |
| Custom | Any OpenAI-compatible API | `https://api.groq.com/openai/v1` |

## User Providers

Users can add their own providers via **Settings > Models**:

- **Link from Global** — Use a global provider with personal API key override
- **Add Custom** — Configure a completely custom provider endpoint

Each user can set one provider as **default** for their chat sessions.

## Configuration via Environment Variables

For Docker deployments, set default providers via environment variables:

| Variable | Description |
|----------|-------------|
| `LLM_PROVIDER` | Default provider type (`ollama` or `openai`) |
| `OPENAI_API_KEY` | OpenAI API key |
| `OPENAI_MODEL` | OpenAI model name |
| `OLLAMA_URL` | Ollama server URL |
| `OLLAMA_MODEL` | Ollama model name |

## Security

- API keys are encrypted with AES-256 at rest
- Keys are never returned in API responses (masked display only)
- Per-user keys are isolated — users cannot see each other's credentials

## Related Resources

- [Getting Started](./getting-started.md) — Initial provider setup
- [Security](./security.md) — Encryption details
