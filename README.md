# OpenClaw.Net

An AI Agent Platform built with .NET, featuring a modular skill system, multi-provider LLM support, and a modern web interface.

## Features

- **Multi-Provider LLM Support**: Ollama, OpenAI, Anthropic, and custom OpenAI-compatible endpoints
- **Multi-Channel Support**: Web UI and Telegram Bot integration
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
│   │   ├── OpenClaw.Api/              # ASP.NET Core Web API
│   │   ├── OpenClaw.Application/      # Business logic, Skills, Pipelines
│   │   ├── OpenClaw.Contracts/        # Interfaces, DTOs, Shared types
│   │   ├── OpenClaw.Domain/           # Domain entities
│   │   ├── OpenClaw.Infrastructure/   # EF Core, External services
│   │   ├── OpenClaw.Channels.Telegram/# Telegram Bot channel adapter
│   │   └── OpenClaw.Hosting/          # Hosting extensions
│   └── skills/
│       └── OpenClaw.Skills.Http/      # HTTP-based skills (Weather, etc.)
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

## Telegram Bot Integration

OpenClaw supports Telegram as a messaging channel. Messages are processed via NATS JetStream for reliable message delivery.

### Setup

1. Create a Telegram Bot via [@BotFather](https://t.me/BotFather) and get your bot token
2. Configure the bot token in Settings UI or via environment variable:

```bash
TELEGRAM__BOTTOKEN=your_bot_token_here
```

3. The bot will automatically start polling for messages when the application starts

### Bot Commands

| Command | Description |
|---------|-------------|
| `/start` | Welcome message |
| `/new` | Start a new conversation |
| `/help` | Show available commands |
| `/skills` | List available skills |
| `/skill_name args` | Execute a skill directly |

### Architecture

```
Telegram API → TelegramChannelAdapter (Polling) → NATS JetStream
                                                        ↓
                                            TelegramEventController
                                                        ↓
                                          HandleTelegramMessageCommand
                                                        ↓
                                              AgentPipeline → LLM
                                                        ↓
                                              TelegramBotClient.SendMessage
```

### Channel Settings

Telegram bot settings can be configured via the Settings UI:
- **Bot Token**: Your Telegram bot token
- **Allowed Chat IDs**: Restrict bot to specific chats (comma-separated, empty = allow all)
- **Enable/Disable**: Toggle the Telegram channel on/off

## Tech Stack

- **Backend**: .NET 10, ASP.NET Core, Entity Framework Core
- **Database**: PostgreSQL
- **Messaging**: NATS JetStream
- **Search**: SearXNG
- **Channels**: Web UI, Telegram Bot
- **Frontend**: Vanilla JS, CSS Variables, marked.js, highlight.js, KaTeX
- **Containerization**: Docker, Docker Compose

## License

MIT