# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-03-30

### Added
- **Audit Logging System** — Persistent audit log for security-critical operations (login, user management, config changes, cron job operations). Includes admin UI with action/date filters, pagination, and auto-cleanup background service. (#9)
- **Per-user Workspace Isolation** — Each user gets an isolated filesystem workspace (`~/.openclaw-net/workspace/{userId}/`). Shared workspace is read-only for non-SuperAdmin. All file system and shell tools enforce workspace boundaries via `PathSecurity`. (#18)
- **Two-layer Model Provider** — Global providers managed by SuperAdmin, per-user providers with own API keys. LLM resolution: User default > Global default > Ollama fallback. (#8)
- **User Ban System** — Permission-based ban with `BanCheckMiddleware` (in-memory cached). Ban/Unban API with reason. Banned users see `banned.html`.
- **CSP Compliance** — All inline scripts and event handlers replaced with external JS + `addEventListener`. Strict `script-src 'self'` enforced.
- **Dashboard Summary Cards** — Admin page shows user/provider statistics at a glance.
- **Error Pages** — Static `404.html`, `error.html`, `banned.html` pages.

### Changed
- **Admin Page** renamed from "User Management" to "Application Settings" with tabs: Pending Approval, All Users, Model Providers, App Config, Audit Log.
- **Home Page** branding updated from "Weda Template" to "OpenClaw".
- **Tabs** restyled to pill/segment style.
- **EF Query Filters** — Removed `Guid.Empty` backdoor; use `IsSuperAdmin` for admin bypass. Added filters for `UserModelProvider`, `UserConfig`, `UserPreference`, `ChannelSettings`.
- **Database Migration** — Switched from `EnsureCreated` to `Database.Migrate()` with legacy baseline detection.
- **Dockerfile** updated to .NET 10 GA images (removed `-preview` tag).

### Fixed
- `AuditLoggingMiddleware` moved after auth middleware so `context.User` is populated.
- `AuditLogController` route segment deduplication (`/audit-log/audit-log` → `/audit-log`).
- `Policy.SuperAdminOnly` constant used instead of incorrect string literal.
- Channel toggle reverts on save failure instead of staying in wrong state.
- Single SuperAdmin enforcement — role not assignable via UI or API.

### Removed
- Unsafe unscoped repository methods (`GetRecentAsync`, `GetByChannelTypeAsync`).
- `docker-compose.override.yml` approach — replaced with `~/.openclaw-net/workspace` bind mount.

## [1.0.0] - 2026-03-29

### Added
- Initial release with multi-user AI agent platform.
- Chat interface with streaming LLM responses.
- CronJob scheduling system with tool instances.
- Telegram channel integration (polling mode).
- Per-user data isolation via EF Core global query filters.
- RBAC: SuperAdmin, Admin, User roles.
- Wiki documentation system with auto-generation.
- Security hardening: CSP headers, login rate limiting, path traversal protection.
