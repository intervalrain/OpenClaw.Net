# ClawOS

**AI Agent Operating System** — Manage Agents, Tools, Channels, and Devices from Cloud to Edge.

Built with C#/.NET, ClawOS provides a unified runtime for AI agents to autonomously execute real-world tasks with enterprise-grade security, multi-user isolation, and extensible plugin architecture.

## Why "OS"?

ClawOS is not just an AI chatbot — it's an **operating system for AI agents**:

| OS Concept | ClawOS |
|-----------|--------|
| Process Management | Agent Pipeline + CronJob Scheduler |
| File System | Tools.FileSystem + Per-user Workspace Isolation |
| IPC | NATS Messaging (Broker + Bus) |
| Device Drivers | Tool Plugins (Shell, Git, HTTP, MQTT...) |
| Networking | Channel Adapters (Web, Telegram, MQTT) |
| User Space | Multi-user RBAC + Encrypted Config Storage |
| Scheduler | Distributed CronJob with Leader Election |
| Package Manager | Skills (SKILL.md) + Tool Auto-discovery |

## Features

### Core
- **Multi-Provider LLM**: Ollama, OpenAI, Anthropic, custom endpoints
- **Two-Layer Model Providers**: Global (SuperAdmin) + Per-user with own API keys
- **Agent Pipeline**: Middleware-based (Logging, Error Handling, Timeout, Secret Redaction)
- **Modular Tool System**: Auto-registered C# plugins via assembly scanning
- **Markdown Skills**: Declarative `SKILL.md` composing tools with LLM instructions
- **CronJob Scheduler**: Distributed scheduling via NATS JetStream + Leader Election
- **Multi-Channel**: Web UI (SSE), Telegram Bot, extensible to MQTT/Line/Discord
- **Real-time Streaming**: Server-Sent Events for chat responses

### Security
- **Per-User Isolation**: All data scoped by authenticated user
- **RBAC**: User / Admin / SuperAdmin with fine-grained permissions
- **JWT Auth**: Token-based with refresh tokens and account lockout
- **AES-256 Encryption**: User secrets and API keys encrypted at rest
- **Audit Logging**: Persistent audit trail for all security-critical operations
- **CSP Enforcement**: Strict Content Security Policy with no inline scripts

### Built-in Tools
Azure DevOps | Notion | GitHub | Web Search (SearXNG) | File System | Shell | Git | PDF | Image Generation (DALL-E) | HTTP | Tmux | Preference

## Architecture

```
ClawOS/
├── src/
│   ├── ClawOS.Api/                # ASP.NET Core API + Static Frontend
│   ├── ClawOS.Application/        # Business logic, Agent pipeline, CronJob executor
│   ├── ClawOS.Contracts/          # Interfaces, DTOs, Tool/Skill/Channel contracts
│   ├── ClawOS.Domain/             # Domain entities (User, CronJob, Device...)
│   ├── ClawOS.Infrastructure/     # EF Core, Security, Persistence
│   ├── ClawOS.Hosting/            # DI composition, LLM/Tool/Channel registration
│   ├── ClawOS.Channels.Telegram/  # Telegram Bot channel adapter
│   └── tools/                     # Built-in tool plugins
├── skills/                        # Markdown-based skill definitions
├── docker-compose.yml
└── Dockerfile
```

## Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started) & Docker Compose

### Run with Docker Compose

```bash
cd ClawOS
cp .env.example .env   # Edit with your values
docker compose up -d
```

| Service | URL |
|---------|-----|
| Web UI | http://localhost:5001 |
| API Docs | http://localhost:5001/swagger |
| SearXNG | http://localhost:8080 |

### First-Time Setup

1. Open http://localhost:5001 — redirects to setup page
2. Create the initial SuperAdmin account
3. Go to **Settings > Models** to configure your LLM provider
4. Start chatting!

### Configuration

| Variable | Description |
|----------|-------------|
| `JWT_SECRET` | JWT signing key (>= 32 chars) |
| `CLAWOS_ENCRYPTION_KEY` | AES-256 key for secrets at rest |
| `LLM_PROVIDER` | Default LLM provider (`ollama` / `openai`) |
| `OPENAI_API_KEY` | OpenAI API key |
| `OLLAMA_URL` | Ollama server URL |

## Development

```bash
cd ClawOS

# Start infrastructure
docker compose up -d postgres nats-broker nats-bus searxng

# Run the API
dotnet run --project src/ClawOS.Api
```

### Creating a Tool

```csharp
public class MyTool(IServiceProvider sp) : AgentToolBase<MyToolArgs>
{
    public override string Name => "my_tool";
    public override string Description => "What this tool does";

    public override async Task<ToolResult> ExecuteAsync(
        MyToolArgs args, ToolContext context, CancellationToken ct)
    {
        return ToolResult.Success("Result");
    }
}

public record MyToolArgs(
    [property: Description("Parameter description")]
    string? Parameter
);
```

Tools placed in `src/tools/` are auto-discovered at startup.

### Creating a Skill

```markdown
---
name: my-skill
description: What this skill does
tools:
  - shell
  - read_file
---

## Instructions

You are a helpful assistant that...
```

Skills in `skills/` are loaded at startup. Invoke via `@my-skill` in chat.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10, ASP.NET Core |
| Database | PostgreSQL (EF Core) |
| Messaging | NATS JetStream |
| Search | SearXNG |
| Observability | OpenTelemetry, Prometheus, Grafana |
| Frontend | Vanilla JS, CSS Variables, marked.js, highlight.js, KaTeX |
| Security | JWT, AES-256, CSP, Audit Logging |
| Container | Docker, Docker Compose |

## Roadmap

- [x] Multi-user Agent Platform with RBAC
- [x] Distributed CronJob Scheduling (NATS)
- [x] Telegram Channel Integration
- [x] Audit Logging & Security Hardening
- [ ] IoT Device Management (MQTT Channel, Device Shadow)
- [ ] Edge Agent (ClawOS.Edge, .NET AOT)
- [ ] Fleet Management & OTA Firmware Updates
- [ ] Agent-to-Agent Communication
- [ ] Skill Marketplace

## License

MIT
