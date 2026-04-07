---
sidebar_position: 7
sidebar_label: 'CronJobs & Automation'
hide_title: true
title: 'CronJobs & Automation'
keywords: ['OpenClaw', 'CronJob', 'Automation', 'Scheduler', 'Tool Instance']
description: 'Schedule and automate AI agent tasks with CronJobs and Tool Instances'
---

# CronJobs & Automation

> Schedule and automate AI agent tasks

## Overview

OpenClaw provides two automation primitives:

- **CronJobs** — Scheduled or manually triggered LLM-powered tasks
- **Tool Instances** — Pre-configured tools with saved arguments for reuse

## CronJobs

### Creating a CronJob

CronJobs are created via the **CronJobs** page in the Web UI or via API. Each CronJob defines:

| Field | Description |
|-------|-------------|
| Name | Display name for the job |
| Content | The prompt/instruction for the LLM |
| Schedule | Cron expression (e.g., `0 9 * * 1-5` for weekdays at 9 AM) |
| Wake Mode | `Scheduled`, `Manual`, or `Both` |
| Context | Optional JSON context (skill binding, variables) |
| Session ID | Optional conversation session to continue |

### Wake Modes

| Mode | Description |
|------|-------------|
| **Scheduled** | Runs automatically on the cron schedule |
| **Manual** | Only runs when explicitly triggered |
| **Both** | Can be triggered manually AND runs on schedule |

### Execution Lifecycle

```
CronJob
  │
  ├── Trigger (Scheduled or Manual)
  │
  ▼
CronJobExecution
  ├── Status: Pending
  ├── Status: Running    ── Agent Pipeline executes
  ├── Status: Completed  ── Output + tool calls stored
  └── Status: Failed     ── Error message stored
      or Cancelled
```

Each execution creates a `CronJobExecution` record with:
- Trigger type (Manual / Scheduled)
- Full output text
- Tool calls JSON
- Error message (if failed)
- Start and completion timestamps

### Skill Binding

CronJobs can reference a Markdown Skill in their context. When executed, the skill's instructions and tool list are injected into the agent pipeline:

```json
{
  "skill": "daily-ado-report"
}
```

## Tool Instances

Tool Instances are pre-configured tools with saved default arguments and a user-facing display name.

### Use Cases

- Save frequently used tool configurations
- Create reusable "actions" without writing code
- Reference in CronJobs for consistent execution

### Example

A Tool Instance named "Check Production API" might pre-configure:
- Tool: `http_request`
- Args: `{ "url": "https://api.example.com/health", "method": "GET" }`

## Agent Activity Tracking

All CronJob executions and chat interactions are logged in the **Agent Activity** system:

| Field | Description |
|-------|-------------|
| Type | `Chat`, `CronJob`, or `ToolExecution` |
| Status | `Started`, `Thinking`, `ToolExecuting`, `Completed`, `Failed` |
| SourceId | Reference to the originating conversation or CronJob |
| Detail | Activity-specific details |

Activities can be viewed on the **Agents** page for monitoring and debugging.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/cron-job` | List CronJobs |
| `POST` | `/api/v1/cron-job` | Create CronJob |
| `PUT` | `/api/v1/cron-job/{id}` | Update CronJob |
| `DELETE` | `/api/v1/cron-job/{id}` | Delete CronJob |
| `POST` | `/api/v1/cron-job/{id}/execute` | Manual execution |
| `GET` | `/api/v1/cron-job/{id}/executions` | List executions |
| `GET/POST/PUT/DELETE` | `/api/v1/tool-instance` | Tool Instance CRUD |

## Related Resources

- [Markdown Skills Guide](./markdown-skills-guide.md) — Skills used in CronJob context
- [API Reference](./api-reference.md) — Full API documentation
