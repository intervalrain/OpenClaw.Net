---
sidebar_position: 5
sidebar_label: 'Markdown Skills'
hide_title: true
title: 'Markdown Skills Guide'
keywords: ['OpenClaw', 'Skill', 'SKILL.md', 'Markdown', 'Declarative']
description: 'Create declarative skills using SKILL.md files that compose tools with LLM instructions'
---

# Markdown Skills Guide

> Declarative skills that compose tools with LLM instructions

## Overview

Markdown Skills are declarative definitions written as `SKILL.md` files. They allow you to compose existing C# tools with custom LLM instructions — no code required. Skills can be invoked via `@skill-name` mention in chat or referenced in CronJob context.

## SKILL.md Format

Create a `SKILL.md` file in the `skills/` directory:

```
skills/
└── my-skill/
    └── SKILL.md
```

### Structure

```markdown
---
name: my-skill
description: What this skill does. Use when user asks for X.
tools: [tool_name_1, tool_name_2]
---

## Instructions

You are a helpful assistant that...

### Steps

1. First, do X
2. Then, do Y
3. Finally, do Z

### Output Format

Describe the expected output format here.

### Rules
- Rule 1
- Rule 2
```

### Frontmatter Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Unique skill identifier (used in `@skill-name` mentions) |
| `description` | string | Yes | What the skill does and when to use it |
| `tools` | string[] | Yes | List of C# tool names this skill can use |

## Example: Daily ADO Report

```markdown
---
name: daily-ado-report
description: Generate a daily work item summary report from Azure DevOps.
  Use when user asks for daily report, sprint status, or work item summary.
tools: [azure_devops, write_file]
---

## Instructions

You are a project management assistant generating a daily work item report.

### Steps

1. Call `azure_devops` with operation `my_work_items` to get current sprint items
2. Analyze the work items and group them by status (Active, New, Resolved, Closed)
3. Generate a concise summary in markdown format

### Output Format

# Daily Report - {date}

## Summary
- Total items: {count}
- Active: {count}

## Active Work Items
| ID | Title | Remaining Work |
|----|-------|---------------|

### Rules
- Keep the report concise
- Highlight items with 0 remaining work that are still Active
- Flag any items that seem stale
```

## Invoking Skills

### In Chat

Mention the skill name with `@` prefix:

```
@daily-ado-report generate today's report
```

### In CronJobs

Reference skills in a CronJob's context to have scheduled executions use the skill's instructions and tools.

## Skill Settings

Skills can be enabled or disabled per workspace via the Skill Settings UI. Disabled skills will not appear in the tool list for agents in that workspace.

## Best Practices

1. **Clear description** — Include trigger phrases ("Use when user asks for...") so the LLM knows when to activate the skill
2. **Specific tools** — Only list the tools the skill actually needs
3. **Structured steps** — Break instructions into numbered steps for consistent execution
4. **Output format** — Define the expected output format so results are consistent
5. **Rules section** — Add constraints and edge case handling

## Related Resources

- [Tool Development Guide](./tool-development-guide.md) — Create the C# tools that skills compose
- [CronJobs & Automation](./cronjobs-automation.md) — Use skills in scheduled tasks
