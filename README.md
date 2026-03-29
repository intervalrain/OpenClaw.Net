# OpenClaw.Net

An enterprise-grade AI Agent Platform built with .NET, featuring multi-user workspace isolation, two-layer model provider management, a modular tool system, and a modern web interface.

## Features

### Core Capabilities
- **Multi-Provider LLM Support**: Ollama, OpenAI, Anthropic, and custom OpenAI-compatible endpoints
- **Two-Layer Model Providers**: SuperAdmin manages global providers; users can add their own with personal API keys
- **Multi-Channel Support**: Web UI and Telegram Bot with per-user channel configuration
- **Modular Tool System**: Auto-registered tools via assembly scanning + Markdown-based Skill definitions
- **CronJob Scheduler**: Scheduled task execution with LLM-assisted argument resolution
- **Real-time Streaming**: SSE (Server-Sent Events) for streaming responses
- **Conversation History**: Persistent chat history with automatic compaction
- **Vision Support**: Image upload and analysis with OpenAI Vision API

### Multi-User & Security
- **Per-User Data Isolation**: All data scoped by authenticated user (conversations, cron jobs, tools, configs, channels)
- **Role-Based Access Control**: Three-tier roles (User / Admin / SuperAdmin) with fine-grained permissions
- **JWT Authentication**: Token-based auth with refresh tokens and account lockout
- **Content Security Policy**: Strict CSP enforcement with no inline scripts
- **Encrypted Config Storage**: AES-256 encryption for user secrets and API keys at rest
- **Security Middleware**: Audit logging, login rate limiting, path traversal protection

### Administration (SuperAdmin)
- **Application Settings**: Unified admin panel for user management, model providers, and app configuration
- **Global Model Providers**: Configure shared LLM providers available to all users
- **App Config Management**: System-wide encrypted configuration (API keys, tokens)
- **User Management**: Approve/reject registrations, manage roles and status

### Integrations (Tools)
- **Azure DevOps**: Work items, repositories, builds, pipelines, and PRs
- **Notion**: Pages, databases, search, and comments
- **GitHub**: Issues, PRs, and CI workflows via GitHub CLI
- **Web Search**: Integrated SearXNG for web search capabilities
- **File System**: Read, write, list with path traversal protection
- **Shell**: Restricted command execution
- **PDF**: Read and search PDF documents
- **Image Generation**: DALL-E integration

## Architecture

```
OpenClaw.Net/
├── OpenClaw/
│   ├── src/
│   │   ├── OpenClaw.Api/                # ASP.NET Core Web API + Static Frontend
│   │   ├── OpenClaw.Application/        # Business logic, CronJob executor, Agent pipeline
│   │   ├── OpenClaw.Contracts/          # Interfaces, DTOs, shared types
│   │   ├── OpenClaw.Domain/             # Domain entities (User, CronJob, ModelProvider, etc.)
│   │   ├── OpenClaw.Infrastructure/     # EF Core, security, persistence
│   │   ├── OpenClaw.Channels.Telegram/  # Telegram Bot channel adapter
│   │   ├── OpenClaw.Hosting/            # DI registration, service extensions
│   │   ├── Weda.Core/                   # Framework core (CQRS, middleware, security)
│   │   └── tools/                       # Built-in tools (FileSystem, Git, Shell, etc.)
│   ├── skills/                          # Markdown-based skill definitions (SKILL.md)
│   ├── docs/                            # Documentation
│   └── docker-compose.yml
```

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started) & Docker Compose

### Run with Docker Compose

```bash
cd OpenClaw

# Configure secrets in .env
cp .env.example .env  # Edit with your values

docker compose up -d
```

Services will be available at:
- **Web UI**: http://localhost:5001
- **API Docs**: http://localhost:5001/swagger
- **SearXNG**: http://localhost:8080

### First-Time Setup

1. Open http://localhost:5001 - you'll be redirected to the setup page
2. Create the initial SuperAdmin account
3. Go to **Settings > Models** to configure your LLM provider
4. Start chatting!

### Configuration

Key environment variables (set in `.env`):

| Variable | Description |
|----------|-------------|
| `JWT_SECRET` | JWT signing key (>= 32 chars, required) |
| `OPENCLAW_ENCRYPTION_KEY` | AES-256 key for encrypting secrets at rest |
| `LLM_PROVIDER` | Default LLM provider (`ollama` or `openai`) |
| `OPENAI_API_KEY` | OpenAI API key (if using OpenAI) |
| `OLLAMA_URL` | Ollama server URL (default: `http://localhost:11434`) |

## Development

### Local Development

```bash
cd OpenClaw

# Start infrastructure services
docker compose up -d postgres nats-broker nats-bus searxng

# Run the API
dotnet run --project src/OpenClaw.Api
```

### Database Migrations

Migrations are **automatically applied** on startup. To create a new migration:

```bash
cd OpenClaw
dotnet ef migrations add MigrationName \
  --project src/OpenClaw.Infrastructure \
  --startup-project src/OpenClaw.Api
```

### Creating a New Tool

Create a class inheriting from `AgentSkillBase<TArgs>`:

```csharp
public class MyTool(IServiceProvider sp) : AgentSkillBase<MyToolArgs>
{
    public override string Name => "my_tool";
    public override string Description => "What this tool does";

    public override async Task<SkillResult> ExecuteAsync(MyToolArgs args, CancellationToken ct)
    {
        return SkillResult.Success("Result");
    }
}

public record MyToolArgs(
    [property: Description("Parameter description")]
    string? Parameter
);
```

Tools are auto-registered via assembly scanning.

### Creating a Markdown Skill

Create a `SKILL.md` file in the `skills/` directory:

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

## API Overview

### Chat & Conversations
- `POST /api/v1/chat/stream` - Stream chat response (SSE)
- `GET/POST/DELETE /api/v1/conversation` - Manage conversations

### Model Providers
- `GET/POST/PUT/DELETE /api/v1/model-provider` - Global providers (SuperAdmin)
- `GET/POST/PUT/DELETE /api/v1/user-model-provider` - User providers
- `GET /api/v1/user-model-provider/available` - List available global providers

### CronJobs
- `GET/POST/PUT/DELETE /api/v1/cron-job` - Manage scheduled jobs
- `POST /api/v1/cron-job/{id}/execute` - Manual execution
- `GET/POST/PUT/DELETE /api/v1/tool-instance` - Manage tool instances

### Configuration
- `GET/PUT/DELETE /api/v1/user-config/{key}` - Per-user encrypted config
- `GET/PUT/DELETE /api/v1/app-config/{key}` - Global app config (SuperAdmin)
- `GET/PUT /api/v1/channel-settings/telegram` - Per-user Telegram settings

### User Management
- `POST /api/v1/auth/login` - Login
- `POST /api/v1/auth/register` - Register
- `GET/POST /api/v1/user-management` - User admin (SuperAdmin)

## Tech Stack

- **Backend**: .NET 10, ASP.NET Core, Entity Framework Core, Mediator (CQRS)
- **Database**: PostgreSQL with auto-migration
- **Messaging**: NATS JetStream
- **Search**: SearXNG
- **Channels**: Web UI, Telegram Bot
- **Frontend**: Vanilla JS (CSP-compliant), CSS Variables, marked.js, highlight.js, KaTeX
- **Security**: JWT, AES-256, CSP, audit logging, rate limiting
- **CI/CD**: GitHub Actions (build, test, dependency vulnerability scan)
- **Containerization**: Docker, Docker Compose

## License

MIT
