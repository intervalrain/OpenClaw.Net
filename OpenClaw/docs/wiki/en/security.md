---
sidebar_position: 8
sidebar_label: 'Security'
hide_title: true
title: 'Security'
keywords: ['OpenClaw', 'Security', 'JWT', 'Encryption', 'RBAC', 'Audit']
description: 'Authentication, authorization, encryption, and audit logging in OpenClaw.NET'
---

# Security

> Authentication, authorization, encryption, and audit logging

## Authentication

### JWT Tokens

OpenClaw uses JWT (JSON Web Token) for authentication:

- Access tokens with configurable expiration
- Refresh token rotation — each refresh issues a new token pair
- Account lockout after failed login attempts

### Registration Flow

1. User registers with email and password
2. SuperAdmin approves the registration (or auto-approve if configured)
3. User receives access upon approval

## Authorization

### Role-Based Access Control (RBAC)

Three-tier role system:

| Role | Capabilities |
|------|-------------|
| **User** | Chat, manage own conversations, CronJobs, tools, and providers |
| **Admin** | All User capabilities + limited admin features |
| **SuperAdmin** | Full system access: user management, global providers, app config, audit logs |

### Permission System

Fine-grained permissions beyond roles:

| Permission | Description |
|-----------|-------------|
| `OpenClaw` | Base access to the platform |
| Custom permissions | Extensible per feature |

### Ban System

- SuperAdmin can ban users with a reason
- `BanCheckMiddleware` enforces ban on every request
- Ban status cached in memory for performance
- Single SuperAdmin enforcement — the last SuperAdmin cannot be banned

## Encryption

### At-Rest Encryption

All sensitive data is encrypted with AES-256:

- User API keys (`UserConfig`)
- Global provider API keys (`ModelProvider`)
- Channel settings (`ChannelSettings`)
- App-level secrets (`AppConfig`)

The encryption key is set via the `OPENCLAW_ENCRYPTION_KEY` environment variable.

### Password Hashing

Passwords are hashed using industry-standard algorithms (bcrypt/PBKDF2) and never stored in plain text.

## Content Security Policy (CSP)

Strict CSP enforcement across all pages:

- No inline scripts or event handlers
- All JavaScript loaded from external files
- Style sources restricted to same-origin and trusted CDNs
- Frame ancestors restricted

## Data Isolation

### User-Level Isolation

- All user-scoped entities implement `IUserScoped`
- EF Core global query filters enforce `WHERE UserId = @currentUserId`
- SuperAdmin can bypass filters for administrative operations

### Filesystem Isolation

- Each user's workspace is an isolated directory
- Path traversal protection prevents access outside workspace
- `DirectoryPermission` entity controls per-path visibility

## Audit Logging

### What is Logged

The `AuditLoggingMiddleware` captures:

| Field | Description |
|-------|-------------|
| UserId / UserEmail | Who performed the action |
| Action | What was done |
| HttpMethod / Path | API endpoint called |
| StatusCode | Response status |
| IpAddress | Client IP |
| UserAgent | Client user agent |
| Timestamp | When it happened |

### Access

- Audit logs are **SuperAdmin-only**
- Available via the Admin UI audit log viewer
- Queryable via API with filtering and pagination

### Retention

Configurable retention policy with automatic cleanup of old entries.

## Rate Limiting

- Login endpoint: rate limited to prevent brute force
- Registration endpoint: rate limited to prevent abuse

## Related Resources

- [Architecture Guide](./architecture.md) — System design
- [Model Providers](./model-providers.md) — API key encryption
