---
sidebar_position: 2
sidebar_label: 'Getting Started'
hide_title: true
title: 'Getting Started'
keywords: ['OpenClaw', 'Setup', 'Docker', 'Installation', 'Quick Start']
description: 'How to install, configure, and run OpenClaw.NET'
---

# Getting Started

> Install, configure, and run OpenClaw.NET in minutes

## Prerequisites

- [Docker](https://www.docker.com/get-started) & Docker Compose
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development only)

## Run with Docker Compose

```bash
cd OpenClaw

# Configure secrets
cp .env.example .env   # Edit with your values

# Start all services
docker compose up -d
```

### Services

| Service | URL |
|---------|-----|
| Web UI | http://localhost:5001 |
| Swagger API Docs | http://localhost:5001/swagger |
| Wiki | http://localhost:5001/wiki |
| SearXNG Search | http://localhost:8080 |

### Environment Variables

Key variables to set in `.env`:

| Variable | Description | Required |
|----------|-------------|----------|
| `JWT_SECRET` | JWT signing key (>= 32 characters) | Yes |
| `OPENCLAW_ENCRYPTION_KEY` | AES-256 key for encrypting secrets at rest | Yes |
| `LLM_PROVIDER` | Default LLM provider (`ollama` or `openai`) | No |
| `OPENAI_API_KEY` | OpenAI API key | If using OpenAI |
| `OLLAMA_URL` | Ollama server URL (default: `http://localhost:11434`) | If using Ollama |

## First-Time Setup

1. Open http://localhost:5001 — you will be redirected to the setup page
2. Create the initial **SuperAdmin** account
3. Navigate to **Settings > Models** and configure your LLM provider
4. Start chatting!

## Local Development

Start only the infrastructure services:

```bash
cd OpenClaw

# Start PostgreSQL, NATS, and SearXNG
docker compose up -d postgres nats-broker nats-bus searxng

# Run the API locally
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

### Running Tests

```bash
cd OpenClaw

# All tests
dotnet test

# Specific test project
dotnet test tests/OpenClaw.Application.UnitTests
dotnet test tests/OpenClaw.Domain.UnitTests
dotnet test tests/OpenClaw.Api.IntegrationTests
```

## Docker Compose Services

The `docker-compose.yml` orchestrates the following services:

| Service | Purpose |
|---------|---------|
| `openclaw-api` | Main application (API + Frontend) |
| `postgres` | PostgreSQL database |
| `nats-broker` | NATS JetStream for messaging |
| `nats-bus` | NATS for event bus |
| `searxng` | SearXNG web search engine |

## Next Steps

- [Architecture Guide](./architecture.md) — Understand the system design
- [Tool Development Guide](./tool-development-guide.md) — Create custom C# tools
- [Markdown Skills Guide](./markdown-skills-guide.md) — Create declarative skills
- [Model Providers](./model-providers.md) — Configure LLM providers