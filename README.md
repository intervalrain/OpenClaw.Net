# OpenClaw.Net

An AI Agent Platform built with .NET, featuring a modular skill system, multi-provider LLM support, and a modern web interface.

## Features

- **Multi-Provider LLM Support**: Ollama, OpenAI, Anthropic, and custom OpenAI-compatible endpoints
- **Modular Skill System**: Auto-registered skills via assembly scanning
- **Slash Commands**: Direct skill invocation with `/skill_name args` syntax
- **Real-time Streaming**: SSE (Server-Sent Events) for streaming responses
- **Conversation History**: Persistent chat history with PostgreSQL
- **Web Search**: Integrated SearXNG for web search capabilities
- **Modern Web UI**: Dark/light theme, autocomplete, and responsive design

## Architecture

```
OpenClaw.Net/
├── OpenClaw/
│   ├── src/
│   │   ├── OpenClaw.Api/           # ASP.NET Core Web API
│   │   ├── OpenClaw.Application/   # Business logic, Skills, Pipelines
│   │   ├── OpenClaw.Contracts/     # Interfaces, DTOs, Shared types
│   │   ├── OpenClaw.Domain/        # Domain entities
│   │   ├── OpenClaw.Infrastructure/# EF Core, External services
│   │   └── OpenClaw.Hosting/       # Hosting extensions
│   └── skills/
│       └── OpenClaw.Skills.Http/   # HTTP-based skills (Weather, etc.)
└── docker-compose.yml
```

## Built-in Skills

| Skill | Description | Slash Command |
|-------|-------------|---------------|
| `weather` | Get weather via wttr.in | `/weather Taipei` |
| `web_search` | Search the web via SearXNG | `/web_search query` |
| `http_request` | Send HTTP GET/POST requests | `/http_request url` |
| `read_file` | Read file contents | `/read_file path` |
| `write_file` | Write content to file | `/write_file path content` |
| `list_directory` | List directory contents | `/list_directory path` |
| `execute_command` | Execute shell commands (restricted) | `/execute_command cmd` |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started) & Docker Compose

### Run with Docker Compose

```bash
cd OpenClaw
docker compose up -d
```

Services will be available at:
- **Web UI**: http://localhost:5001/openclaw/
- **API**: http://localhost:5001/api/v1/
- **SearXNG**: http://localhost:8080

### Configuration

1. Open the web UI and go to **Settings**
2. Add a model provider (Ollama, OpenAI, or Anthropic)
3. Start chatting!

## Development

### Local Development

```bash
cd OpenClaw

# Restore dependencies
dotnet restore

# Run the API (requires Docker services)
docker compose up -d postgres nats-broker searxng
dotnet run --project src/OpenClaw.Api
```

### Database Migrations

```bash
cd OpenClaw/src/OpenClaw.Infrastructure

# Add migration
dotnet ef migrations add MigrationName -s ../OpenClaw.Api

# Apply migrations
dotnet ef database update -s ../OpenClaw.Api
```

### Creating a New Skill

1. Create a new class inheriting from `AgentSkillBase<TArgs>`:

```csharp
public class MySkill(IServiceProvider sp) : AgentSkillBase<MySkillArgs>
{
    public override string Name => "my_skill";
    public override string Description => "Description of what this skill does";

    public override async Task<SkillResult> ExecuteAsync(MySkillArgs args, CancellationToken ct)
    {
        // Implementation
        return SkillResult.Success("Result");
    }
}

public record MySkillArgs(
    [property: Description("Parameter description")]
    string? Parameter
);
```

2. The skill will be auto-registered via assembly scanning.

## API Endpoints

### Chat
- `POST /api/v1/chat/stream` - Stream chat response (SSE)

### Conversations
- `GET /api/v1/conversation` - List conversations
- `POST /api/v1/conversation` - Create conversation
- `GET /api/v1/conversation/{id}` - Get conversation with messages
- `DELETE /api/v1/conversation/{id}` - Delete conversation

### Model Providers
- `GET /api/v1/model-provider` - List providers
- `POST /api/v1/model-provider` - Create provider
- `POST /api/v1/model-provider/{id}/activate` - Set active provider
- `DELETE /api/v1/model-provider/{id}` - Delete provider

### Skills
- `GET /api/v1/skill-settings` - List all skills with enable status
- `POST /api/v1/skill-settings/{name}/enable` - Enable skill
- `POST /api/v1/skill-settings/{name}/disable` - Disable skill

## Tech Stack

- **Backend**: .NET 10, ASP.NET Core, Entity Framework Core
- **Database**: PostgreSQL
- **Messaging**: NATS
- **Search**: SearXNG
- **Frontend**: Vanilla JS, CSS Variables, marked.js, highlight.js, KaTeX
- **Containerization**: Docker, Docker Compose

## License

MIT