---
sidebar_position: 9
sidebar_label: 'API Reference'
hide_title: true
title: 'API Reference'
keywords: ['OpenClaw', 'API', 'REST', 'Endpoints', 'Swagger']
description: 'REST API endpoints reference for OpenClaw.NET'
---

# API Reference

> REST API endpoints (v1.0)

Full interactive documentation is available at `/swagger` when the server is running.

## Authentication

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/auth/login` | Login with email and password |
| `POST` | `/api/v1/auth/register` | Register a new account |
| `POST` | `/api/v1/auth/refresh` | Refresh access token |
| `POST` | `/api/v1/auth/logout` | Logout and invalidate refresh token |

All other endpoints require a valid JWT token in the `Authorization: Bearer <token>` header.

## Chat & Conversations

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/chat/stream` | Stream chat response (SSE) |
| `GET` | `/api/v1/conversation` | List conversations |
| `POST` | `/api/v1/conversation` | Create conversation |
| `GET` | `/api/v1/conversation/{id}` | Get conversation with messages |
| `DELETE` | `/api/v1/conversation/{id}` | Delete conversation |

## Model Providers

### Global Providers (SuperAdmin)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/model-provider` | List global providers |
| `POST` | `/api/v1/model-provider` | Create global provider |
| `PUT` | `/api/v1/model-provider/{id}` | Update global provider |
| `DELETE` | `/api/v1/model-provider/{id}` | Delete global provider |

### User Providers

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/user-model-provider` | List user's providers |
| `POST` | `/api/v1/user-model-provider` | Add user provider |
| `PUT` | `/api/v1/user-model-provider/{id}` | Update user provider |
| `DELETE` | `/api/v1/user-model-provider/{id}` | Delete user provider |
| `GET` | `/api/v1/user-model-provider/available` | List available global providers |

## CronJobs

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/cron-job` | List CronJobs |
| `POST` | `/api/v1/cron-job` | Create CronJob |
| `PUT` | `/api/v1/cron-job/{id}` | Update CronJob |
| `DELETE` | `/api/v1/cron-job/{id}` | Delete CronJob |
| `POST` | `/api/v1/cron-job/{id}/execute` | Trigger manual execution |
| `GET` | `/api/v1/cron-job/{id}/executions` | List execution history |

## Tool Instances

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/tool-instance` | List tool instances |
| `POST` | `/api/v1/tool-instance` | Create tool instance |
| `PUT` | `/api/v1/tool-instance/{id}` | Update tool instance |
| `DELETE` | `/api/v1/tool-instance/{id}` | Delete tool instance |

## Configuration

### User Config (Encrypted)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/user-config` | List user config keys |
| `GET` | `/api/v1/user-config/{key}` | Get config value |
| `PUT` | `/api/v1/user-config/{key}` | Set config value |
| `DELETE` | `/api/v1/user-config/{key}` | Delete config entry |

### App Config (SuperAdmin)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/app-config` | List app config keys |
| `PUT` | `/api/v1/app-config/{key}` | Set app config value |
| `DELETE` | `/api/v1/app-config/{key}` | Delete app config entry |

### Channel Settings

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/channel-settings/telegram` | Get Telegram settings |
| `PUT` | `/api/v1/channel-settings/telegram` | Update Telegram settings |

## User Management (SuperAdmin)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/user-management` | List all users |
| `POST` | `/api/v1/user-management/{id}/approve` | Approve registration |
| `POST` | `/api/v1/user-management/{id}/ban` | Ban user |
| `POST` | `/api/v1/user-management/{id}/unban` | Unban user |
| `PUT` | `/api/v1/user-management/{id}/role` | Update user role |

## Workspaces

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/workspace` | List workspaces |
| `POST` | `/api/v1/workspace` | Create workspace |
| `GET` | `/api/v1/workspace/{id}` | Get workspace details |
| `PUT` | `/api/v1/workspace/{id}` | Update workspace |
| `GET` | `/api/v1/workspace/{id}/files` | List workspace files |

## Agents & Activities

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/agents` | List agents |
| `GET` | `/api/v1/agent-activity` | List agent activities |
| `GET` | `/api/v1/agent-activity/stream` | Stream activities (SSE) |

## Audit Logs (SuperAdmin)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/audit-log` | Query audit logs with filtering |

## Setup

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/setup/status` | Check if system is initialized |
| `POST` | `/api/v1/setup/initialize` | Create initial SuperAdmin account |

## Related Resources

- [Getting Started](./getting-started.md) — Setup and access
- [Security](./security.md) — Authentication details
