---
sidebar_position: 1
sidebar_label: 'Introduction'
hide_title: true
title: 'OpenClaw.NET - AI Agent Platform'
keywords: ['OpenClaw', 'AI Agent', '.NET', 'LLM', 'Enterprise Platform']
description: 'An enterprise-grade AI Agent Platform built with .NET, featuring multi-user isolation, modular tools, and scheduled automation'
---

# OpenClaw.NET

> An enterprise-grade AI Agent Platform built with .NET

## Overview

OpenClaw.NET is a self-hosted AI Agent Platform that enables LLM-powered agents to execute real-world tasks through a modular tool system. Built with Clean Architecture and Domain-Driven Design, it provides multi-user workspace isolation, two-layer model provider management, scheduled automation, and a comprehensive security infrastructure.

## Key Features

### AI Agent Capabilities
- **Multi-Provider LLM Support** - Ollama, OpenAI, Anthropic, and any OpenAI-compatible endpoint
- **Two-Layer Model Providers** - SuperAdmin manages global providers; users can add their own with personal API keys
- **Modular Tool System** - 12 built-in C# tools with auto-registration via assembly scanning
- **Markdown Skills** - Declarative skill definitions (`SKILL.md`) that compose tools with LLM instructions
- **Real-time Streaming** - SSE (Server-Sent Events) for streaming chat responses
- **Context Compaction** - Automatic conversation history compression for long sessions
- **Vision Support** - Image upload and analysis via OpenAI Vision API

### Multi-User & Workspaces
- **Per-User Data Isolation** - All data scoped by authenticated user via EF Core global query filters
- **Workspace Management** - Personal and shared workspaces with role-based access (Viewer / Member / Owner)
- **Filesystem Isolation** - Each user scoped to their own workspace directory with path traversal protection
- **Directory Permissions** - Configurable visibility (Default / Public / PublicReadonly / Private) per directory

### Automation
- **CronJob Scheduler** - Cron-expression based scheduling with LLM-assisted execution
- **Tool Instances** - Pre-configured tools with user-facing names for reuse in CronJobs
- **Agent Activity Tracking** - Append-only activity log for monitoring and visualization

### Security
- **JWT Authentication** - Token-based auth with refresh token rotation and account lockout
- **Role-Based Access Control** - Three-tier roles (User / Admin / SuperAdmin) with fine-grained permissions
- **Encrypted Storage** - AES-256 encryption for API keys and secrets at rest
- **Content Security Policy** - Strict CSP enforcement with no inline scripts
- **Audit Logging** - All security-critical operations logged (SuperAdmin-only viewer)
- **Rate Limiting** - Login and registration rate limiting

### Integrations
- **Telegram Bot** - Channel adapter with per-user configuration
- **Azure DevOps** - Work items, repositories, builds, pipelines, and PRs
- **GitHub** - Issues, PRs, and CI workflows via GitHub CLI
- **Notion** - Pages, databases, search, and comments
- **Web Search** - Integrated SearXNG for web search
- **And more** - Shell, HTTP, PDF, Image Generation, Tmux

## Architecture

```
                    +------------------+
                    |   Channel Layer  |
                    |  Web UI / Telegram|
                    +--------+---------+
                             |
                    +--------v---------+
                    |    API Layer     |
                    |  ASP.NET Core   |
                    |  (Controllers)  |
                    +--------+---------+
                             |
                    +--------v---------+
                    | Application Layer|
                    |  CQRS + Mediator |
                    |  Agent Pipeline  |
                    +--------+---------+
                             |
              +--------------+--------------+
              |              |              |
     +--------v---+  +------v------+  +----v--------+
     |   Domain   |  |Infrastructure|  |    Tools    |
     |  Entities  |  | EF Core, JWT |  | 12 modules  |
     |  Aggregates|  | NATS, Email  |  | FileSystem  |
     +------------+  +-------------+  | Git, Shell..|
                                      +-------------+
```

## Project Structure

```
OpenClaw/
├── src/
│   ├── OpenClaw.Api/                    # REST API + Static Frontend
│   ├── OpenClaw.Application/            # Business logic, CQRS, Agent Pipeline
│   ├── OpenClaw.Contracts/              # Interfaces, DTOs, shared types
│   ├── OpenClaw.Domain/                 # Domain entities, aggregates, repositories
│   ├── OpenClaw.Infrastructure/         # EF Core, security, persistence, services
│   ├── OpenClaw.Channels.Telegram/      # Telegram Bot channel adapter
│   ├── OpenClaw.Hosting/                # DI registration, observability
│   ├── Weda.Core/                       # Shared framework (CQRS, NATS, security)
│   └── tools/                           # 12 built-in tool modules
│       ├── OpenClaw.Tools.FileSystem/
│       ├── OpenClaw.Tools.Git/
│       ├── OpenClaw.Tools.GitHub/
│       ├── OpenClaw.Tools.AzureDevOps/
│       ├── OpenClaw.Tools.Shell/
│       ├── OpenClaw.Tools.Http/
│       ├── OpenClaw.Tools.WebSearch/
│       ├── OpenClaw.Tools.Pdf/
│       ├── OpenClaw.Tools.ImageGen/
│       ├── OpenClaw.Tools.Notion/
│       ├── OpenClaw.Tools.Tmux/
│       └── OpenClaw.Tools.Preference/   # User preference management
├── skills/                              # Markdown skill definitions (SKILL.md)
├── tests/                               # Unit, integration, playground tests
├── docs/                                # Documentation & Wiki
└── docker-compose.yml                   # Full stack orchestration
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| Web Framework | ASP.NET Core + Mediator (CQRS) |
| Database | PostgreSQL + Entity Framework Core |
| Messaging | NATS JetStream |
| Security | JWT, AES-256, CSP, Audit Logging |
| Frontend | Vanilla JavaScript (CSP-compliant) |
| Search | SearXNG |
| Observability | OpenTelemetry |
| Containerization | Docker + Docker Compose |

## Related Resources

- [Getting Started](./getting-started.md)
- [Architecture Guide](./architecture.md)
- [Tool Development Guide](./tool-development-guide.md)
- [Markdown Skills Guide](./markdown-skills-guide.md)
- [Model Providers](./model-providers.md)
- [CronJobs & Automation](./cronjobs-automation.md)
- [Security](./security.md)
- [API Reference](./api-reference.md)