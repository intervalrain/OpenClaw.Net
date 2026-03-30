# OpenClaw.Net Development Progress

## Current Sprint (2026-03-30)

### In Progress
- [ ] **#9 Audit Logging System** — `feature/audit-logging`
  - [ ] AuditLog entity + migration
  - [ ] AuditLog repository + query API (SuperAdmin only)
  - [ ] Persist audit events from existing AuditLoggingMiddleware
  - [ ] Admin UI: Audit Log viewer tab
  - [ ] Retention policy (auto-cleanup)

### Completed (this sprint)
- [x] **#14 WikiGenerator CSP Compliance** — `fix/wiki-csp-compliance` → merged via PR #17
- [x] **User Isolation** — `fix/user-isolation` → PR #18
  - [x] Query filter hardening (remove Guid.Empty backdoor)
  - [x] Per-user workspace filesystem isolation
  - [x] ToolContext userId propagation
  - [x] Remove unsafe unscoped repository methods
- [x] **Two-layer Model Provider** — `feature/workflow` → merged via PR #8
  - [x] Global + per-user model provider architecture
  - [x] Per-user LLM provider resolution
  - [x] Model provider management UI (user + admin)
- [x] **Ban System** — `fix/wiki-csp-compliance`
  - [x] Permission-based ban enforcement with BanCheckMiddleware
  - [x] Single SuperAdmin enforcement
  - [x] Ban/Unban UI with reason
- [x] **CSP Compliance** — inline scripts/handlers removed across all pages
- [x] **Auto-migrate** — EnsureCreated → Migrate with legacy baseline detection
- [x] **Per-user Channel Settings** — Telegram settings scoped to user

## Backlog (by priority)

### P0
- [ ] **#19** CronJob 分散式排程與執行 (NATS JetStream)

### P1
- [ ] **#20** DAG Workflow Executor NATS 分散式任務分發
- [ ] **#9** Audit Logging System ← **next**
- [ ] **#11** Multi-tenant Workspace System (entity + members)

### P2
- [ ] **#21** Channel Adapter 事件匯流 (Telegram/Slack/Discord → NATS)
- [ ] **#10** Channel User Binding (外部平台帳號映射)
- [ ] **#13** Rate Limiting and User Quota
- [ ] **#16** User Usage Tracking & Statistics Dashboard
- [ ] **#12** Email Verification and Password Reset

### P3
- [ ] **#22** Hierarchical Agent 間 NATS 通訊
- [ ] **#15** Hierarchical Agent Architecture (HAA)
- [ ] **#7** Update Notification System

### User Stories
- [ ] **#1** US-1: Multi-Channel Communication (LINE/Teams/Discord/Slack)
- [ ] **#2** US-2: Browser Automation with Live View
- [ ] **#3** US-3: Preference Learning from Conversation
- [ ] **#4** US-4: Food Ordering Automation
- [ ] **#5** US-5: ADO Task Automation
