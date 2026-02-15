---
sidebar_position: 1
sidebar_label: 'Introduction'
hide_title: true
title: 'OpenClaw.NET - AI Agent Runtime'
keywords: ['OpenClaw', 'AI Agent', '.NET', 'LLM', 'Plugin Architecture']
description: 'A strongly-typed, plugin-oriented AI Agent Runtime built with C#/.NET'
---

# OpenClaw.NET

> A strongly-typed, plugin-oriented AI Agent Runtime built with C#/.NET

## Overview

OpenClaw.NET is an enterprise-grade AI Agent Platform that enables LLM-powered agents to execute real-world tasks through a pluggable skill system. It focuses on engineering quality, type safety, and long-term maintainability.

## Key Features

- **Strongly Typed** - All Skills are compile-time checked
- **Plugin Architecture** - Extend capabilities via NuGet packages
- **LLM Agnostic** - Support multiple LLM providers (Ollama, OpenAI, etc.)
- **Pipeline-based** - Middleware pattern for request processing

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      OpenClaw.NET Architecture                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│    [ Channel Adapter ]      CLI, WebSocket, Telegram...         │
│            │                                                    │
│            ▼                                                    │
│    [ Agent Pipeline ]       Middleware chain                    │
│            │                                                    │
│            ▼                                                    │
│    [ LLM Provider ]         Ollama, OpenAI, Azure...            │
│            │                                                    │
│            ▼                                                    │
│    [ Skill Executor ]       FileSystem, Shell, HTTP...          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
OpenClaw/
├── src/
│   ├── OpenClaw.Contracts/                  # Interfaces & DTOs (SPI for Skills)
│   ├── OpenClaw.Application/                # Agent Pipeline, Use Cases
│   ├── OpenClaw.Domain/                     # Domain Models
│   ├── OpenClaw.Infrastructure/             # Database, External Services
│   ├── OpenClaw.Infrastructure.Llm.Ollama/  # Ollama Provider
│   ├── OpenClaw.Api/                        # REST API
│   └── OpenClaw.Cli/                        # CLI Application
├── skills/
│   └── OpenClaw.Skills.FileSystem/          # File operations (read, write, list)
├── tests/
└── docs/
```

## Quick Start

### Prerequisites

- Docker & Docker Compose
- .NET 10 SDK (for local development)
- Ollama with a model installed (e.g., `ollama pull qwen2.5:7b`)

### Run with Docker Compose (Recommended)

Start all services (API + PostgreSQL):

```bash
docker compose up -d
```

View logs:

```bash
docker compose logs -f
```

Stop services:

```bash
docker compose down
```

### Access Points

| Service | URL |
|---------|-----|
| API | http://localhost:5001 |
| Swagger UI | http://localhost:5001/swagger |
| Wiki | http://localhost:5001/wiki |

### Local Development

Start only PostgreSQL container:

```bash
docker compose up -d postgres
```

Run API locally:

```bash
dotnet run --project src/OpenClaw.Api
```

### Run CLI

```bash
cd OpenClaw
dotnet run --project src/OpenClaw.Cli
```

### Example Interaction

```
> list files in current directory

The files in the current directory are:
- README.md
- src/
- docs/
...
```

## Creating a Custom Skill

```csharp
public class MySkill : AgentSkillBase<MySkillArgs>
{
    public override string Name => "my_skill";
    public override string Description => "Does something useful";

    protected override Task<SkillResult> ExecuteAsync(MySkillArgs args, CancellationToken ct)
    {
        // Implementation
        return Task.FromResult(SkillResult.Success("Done!"));
    }
}

public record MySkillArgs(
    [property: Description("Parameter description")]
    string? Param1
);
```

See [Agent Skill Development Guide](./agent-skill-guide.md) for details.

---

## Related Resources

- [Agent Skill Development Guide](./agent-skill-guide.md)

import Revision from '@site/src/components/Revision';

<Revision date="Feb-15, 2026" version="v1.0.0" />