---
sidebar_position: 4
sidebar_label: 'Tool Development'
hide_title: true
title: 'Tool Development Guide'
keywords: ['OpenClaw', 'Tool', 'Plugin', 'Development', 'C#']
description: 'Learn how to create custom C# tools for OpenClaw.NET'
---

# Tool Development Guide

> Create custom C# tools that extend agent capabilities

## Overview

Tools are strongly-typed C# classes that provide executable capabilities to AI agents. Each tool represents a specific action (e.g., reading files, making HTTP requests, executing shell commands). Tools are auto-registered via assembly scanning — just place them under `src/tools/` and they are available to agents.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Your Tool Project                         │
│              OpenClaw.Tools.MyTools                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│    ┌─────────────────────────────────────────────────┐      │
│    │     YourTool : AgentToolBase<YourToolArgs>       │      │
│    └─────────────────────────────────────────────────┘      │
│                            │                                │
│                            ▼                                │
│    ┌─────────────────────────────────────────────────┐      │
│    │          OpenClaw.Contracts (NuGet)              │      │
│    │  - IAgentTool                                    │      │
│    │  - AgentToolBase<TArgs>                          │      │
│    │  - SkillResult, SkillContext                     │      │
│    └─────────────────────────────────────────────────┘      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

Tools depend only on `OpenClaw.Contracts`, keeping them lightweight and distributable.

## Quick Start

### 1. Create a Tool Project

```bash
cd OpenClaw/src/tools
dotnet new classlib -n OpenClaw.Tools.MyTools
cd OpenClaw.Tools.MyTools
dotnet add reference ../../OpenClaw.Contracts/OpenClaw.Contracts.csproj
```

### 2. Define Your Tool

```csharp
using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.MyTools;

public class GreetTool(IServiceProvider sp) : AgentToolBase<GreetToolArgs>
{
    public override string Name => "greet";
    public override string Description => "Greet a person by name.";

    public override async Task<SkillResult> ExecuteAsync(
        GreetToolArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Name))
            return SkillResult.Failure("Name is required.");

        return SkillResult.Success($"Hello, {args.Name}!");
    }
}

public record GreetToolArgs(
    [property: Description("The name of the person to greet")]
    string? Name
);
```

### 3. Register in the Solution

Add a project reference in `OpenClaw.Api.csproj`:

```xml
<ProjectReference Include="..\tools\OpenClaw.Tools.MyTools\OpenClaw.Tools.MyTools.csproj" />
```

The tool is automatically discovered and registered via assembly scanning. No manual registration needed.

## Key Concepts

### AgentToolBase<TArgs>

The generic base class that handles:
- **JSON Schema generation** from `TArgs` record properties
- **Argument deserialization** from LLM tool call responses
- **Error handling** and result wrapping
- **Dependency injection** via constructor `IServiceProvider`

### TArgs Record

Define your tool's parameters as a C# `record`:

```csharp
public record MyToolArgs(
    [property: Description("Required parameter")]
    string RequiredParam,

    [property: Description("Optional parameter")]
    string? OptionalParam,

    [property: Description("Numeric parameter")]
    int? Count
);
```

**Type Mapping to JSON Schema:**

| C# Type | JSON Schema Type |
|---------|------------------|
| `string` | `string` |
| `int`, `long`, `short` | `integer` |
| `float`, `double`, `decimal` | `number` |
| `bool` | `boolean` |
| `T[]`, `List<T>` | `array` |

**Nullable types** (`string?`, `int?`) are treated as optional parameters.

### SkillResult

Return execution results to the LLM:

```csharp
// Success with text output
return SkillResult.Success("Operation completed.");

// Success with structured data
return SkillResult.Success(JsonSerializer.Serialize(data));

// Failure with error message
return SkillResult.Failure("File not found.");
```

### Accessing Services

Use the constructor-injected `IServiceProvider` to resolve services:

```csharp
public class MyTool(IServiceProvider sp) : AgentToolBase<MyToolArgs>
{
    public override async Task<SkillResult> ExecuteAsync(
        MyToolArgs args, CancellationToken ct)
    {
        var dbContext = sp.GetRequiredService<AppDbContext>();
        var httpClient = sp.GetRequiredService<IHttpClientFactory>();
        // ...
    }
}
```

## Best Practices

### 1. Use Descriptive Names

```csharp
// snake_case, verb + noun
public override string Name => "read_file";
public override string Description =>
    "Read the contents of a file at the specified path.";
```

### 2. Validate Input Early

```csharp
public override async Task<SkillResult> ExecuteAsync(
    MyToolArgs args, CancellationToken ct)
{
    if (string.IsNullOrEmpty(args.Path))
        return SkillResult.Failure("Path is required.");

    if (!File.Exists(args.Path))
        return SkillResult.Failure($"File not found: {args.Path}");

    // Continue...
}
```

### 3. Handle Cancellation

```csharp
public override async Task<SkillResult> ExecuteAsync(
    MyToolArgs args, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    var content = await File.ReadAllTextAsync(args.Path, ct);
    return SkillResult.Success(content);
}
```

### 4. Respect Path Security

For tools that access the filesystem, use the path security utilities:

```csharp
var pathSecurity = sp.GetRequiredService<IPathSecurity>();
if (!pathSecurity.IsPathAllowed(args.Path))
    return SkillResult.Failure("Access denied: path is outside workspace.");
```

## Built-in Tools Reference

| Tool | Name | Description |
|------|------|-------------|
| FileSystem | `read_file`, `write_file`, `list_directory` | File operations with path traversal protection |
| Git | `git` | Git operations (diff, log, status, clone) |
| GitHub | `github` | PR/issue management via GitHub CLI |
| AzureDevOps | `azure_devops` | Work items, repos, builds, PRs |
| Shell | `execute_command` | Restricted command execution |
| Http | `http_request` | HTTP requests (GET/POST/PUT) |
| WebSearch | `web_search` | SearXNG web search |
| Pdf | `read_pdf`, `search_pdf` | PDF reading and search |
| ImageGen | `generate_image` | DALL-E image generation |
| Notion | `notion` | Page/database operations |
| Tmux | `tmux` | Terminal multiplexer control |
| Preference | `preference` | User preference management |

## Related Resources

- [Markdown Skills Guide](./markdown-skills-guide.md) — Declarative skills that compose tools
- [Architecture Guide](./architecture.md) — System design overview
