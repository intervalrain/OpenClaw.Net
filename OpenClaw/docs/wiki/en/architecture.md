---
sidebar_position: 3
sidebar_label: 'Architecture'
hide_title: true
title: 'Architecture Guide'
keywords: ['OpenClaw', 'Architecture', 'Clean Architecture', 'DDD', 'CQRS']
description: 'System architecture, design patterns, and domain model of OpenClaw.NET'
---

# Architecture Guide

> Clean Architecture, CQRS, and Domain-Driven Design

## Design Principles

OpenClaw.NET follows these architectural patterns:

- **Clean Architecture** — Dependency flows inward: Domain has no external dependencies
- **Domain-Driven Design (DDD)** — Rich domain entities with aggregate roots
- **CQRS** — Commands and queries separated via Mediator pattern
- **Multi-User Isolation** — EF Core global query filters ensure data is scoped per user

## Layer Overview

```
┌────────────────────────────────────────────────────────────────┐
│                        Presentation                            │
│    Controllers (36)  ·  SSE Streaming  ·  Swagger  ·  Wiki    │
├────────────────────────────────────────────────────────────────┤
│                        Application                             │
│    Commands / Queries / Handlers  ·  Agent Pipeline            │
│    Tool Registry  ·  Skill Settings  ·  Context Compressor    │
├────────────────────────────────────────────────────────────────┤
│                          Domain                                │
│    Entities (22)  ·  Aggregate Roots  ·  Value Objects         │
│    Repository Interfaces  ·  Domain Events                     │
├────────────────────────────────────────────────────────────────┤
│                       Infrastructure                           │
│    EF Core  ·  Repositories  ·  JWT Auth  ·  AES Encryption   │
│    NATS Messaging  ·  Email  ·  Audit Logging                 │
├────────────────────────────────────────────────────────────────┤
│                          Tools                                 │
│    FileSystem · Git · GitHub · AzureDevOps · Shell · Http     │
│    WebSearch · Pdf · ImageGen · Notion · Tmux · Preference    │
└────────────────────────────────────────────────────────────────┘
```

## Domain Model

### Authentication & Users

| Entity | Purpose |
|--------|---------|
| **User** | Core user account with email, roles, permissions, workspace path |
| **UserPreference** | Key-value user preferences |
| **UserConfig** | Encrypted user secrets (API keys, tokens) |
| **EmailVerification** | Email verification flow |
| **PasswordResetToken** | Password reset flow |

### Chat & Conversations

| Entity | Purpose |
|--------|---------|
| **Conversation** | Chat session (aggregate root) with message history |
| **ConversationMessage** | Individual message with role (User/Assistant) and content |

### Agent Execution

| Entity | Purpose |
|--------|---------|
| **CronJob** | Scheduled/manual job with cron expression, context, and skill binding |
| **CronJobExecution** | Single execution record with status, output, and tool calls |
| **ToolInstance** | Pre-configured tool with display name and default arguments |
| **AgentActivity** | Append-only activity log (Chat/CronJob/ToolExecution) |

### Configuration

| Entity | Purpose |
|--------|---------|
| **ModelProvider** | Global LLM provider (SuperAdmin-managed) |
| **UserModelProvider** | Per-user LLM provider (own or linked from global) |
| **AppConfig** | Application-level configuration |
| **ChannelSettings** | Per-user channel configuration (e.g., Telegram) |

### Workspaces & Collaboration

| Entity | Purpose |
|--------|---------|
| **Workspace** | Team or personal workspace |
| **WorkspaceMember** | Membership with role (Viewer/Member/Owner) |
| **DirectoryPermission** | Filesystem path visibility per user |
| **SkillSetting** | Per-workspace skill enable/disable toggle |

### Integration & Audit

| Entity | Purpose |
|--------|---------|
| **ChannelUserBinding** | External platform account mapping |
| **Notification** | User notifications |
| **AuditLog** | Security audit trail (SuperAdmin only) |

## Agent Pipeline

The Agent Pipeline is the core execution engine for LLM interactions:

```
User Message
    │
    ▼
┌──────────────┐
│ Slash Command│ ── @skill-name mention parsing
│    Parser    │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│   Context    │ ── Conversation history + skill instructions
│  Assembler   │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  LLM Provider│ ── Ollama / OpenAI / Custom
│   Factory    │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│   Tool Call  │ ── Tool Registry resolves and executes
│   Executor   │    tool calls from LLM response
└──────┬───────┘
       │
       ▼
┌──────────────┐
│    Context   │ ── Compress history when approaching
│  Compressor  │    token limits
└──────┬───────┘
       │
       ▼
  SSE Response Stream
```

## Key Application Services

| Service | Responsibility |
|---------|---------------|
| `IAgentPipeline` | Main LLM execution engine |
| `IToolRegistry` | Tool/skill discovery and invocation |
| `ILlmProviderFactory` | Provider resolution (user > global > fallback) |
| `IContextCompressor` | Token usage optimization |
| `ISlashCommandParser` | Skill mention parsing (`@skill-name`) |
| `CronJobSchedulerService` | Background scheduler with leader election |

## Data Isolation

Multi-user data isolation is enforced at the database level:

1. All user-scoped entities implement `IUserScoped` interface
2. EF Core global query filters automatically add `WHERE UserId = @currentUserId`
3. SuperAdmin can bypass filters for administrative operations
4. Workspace filesystem paths are isolated per user with path traversal protection

## Related Resources

- [Tool Development Guide](./tool-development-guide.md)
- [Security](./security.md)
- [API Reference](./api-reference.md)