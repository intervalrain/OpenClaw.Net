---
sidebar_position: 2
sidebar_label: 'Agent Skill Guide'
hide_title: true
title: 'Agent Skill Development Guide'
keywords: ['OpenClaw', 'Skill', 'Plugin', 'Development', 'Tutorial']
description: 'Learn how to create custom Skills for OpenClaw.NET'
---

# Agent Skill Development Guide

> Learn how to create custom Skills for OpenClaw.NET

## Overview

Skills are the building blocks that enable AI Agents to interact with the real world. Each Skill represents a specific capability (e.g., reading files, making HTTP requests, executing shell commands).

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       Your Skill NuGet                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│    ┌─────────────────────────────────────────────────────┐      │
│    │      YourSkill : AgentSkillBase<YourSkillArgs>      │      │
│    └─────────────────────────────────────────────────────┘      │
│                              │                                  │
│                              ▼                                  │
│    ┌─────────────────────────────────────────────────────┐      │
│    │            OpenClaw.Contracts (NuGet)               │      │
│    │  - IAgentSkill                                      │      │
│    │  - AgentSkillBase<TArgs>                            │      │
│    │  - SkillResult, SkillContext                        │      │
│    └─────────────────────────────────────────────────────┘      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

Skills only depend on `OpenClaw.Contracts`, making them lightweight and easy to distribute.

## Quick Start

### 1. Create a new Class Library

```bash
dotnet new classlib -n OpenClaw.Skills.MySkills
cd OpenClaw.Skills.MySkills
dotnet add package OpenClaw.Contracts
```

### 2. Define Your Skill

```csharp
using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.MySkills;

public class GreetSkill : AgentSkillBase<GreetSkillArgs>
{
    public override string Name => "greet";
    public override string Description => "Greet a person by name.";

    protected override Task<SkillResult> ExecuteAsync(GreetSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Name))
        {
            return Task.FromResult(SkillResult.Failure("Name is required."));
        }

        var greeting = $"Hello, {args.Name}!";
        return Task.FromResult(SkillResult.Success(greeting));
    }
}

public record GreetSkillArgs(
    [property: Description("The name of the person to greet")]
    string? Name
);
```

### 3. Register with Agent Pipeline

```csharp
var skills = new IAgentSkill[]
{
    new GreetSkill(),
    // ... other skills
};

var pipeline = new AgentPipeline(llmProvider, skills, options);
```

## Key Concepts

### AgentSkillBase<TArgs>

The generic base class that handles:
- JSON Schema generation from `TArgs` properties
- Argument deserialization
- Error handling

### TArgs Record

Define your skill's parameters as a `record`:

```csharp
public record MySkillArgs(
    [property: Description("Required parameter")]
    string RequiredParam,

    [property: Description("Optional parameter")]
    string? OptionalParam,

    [property: Description("Numeric parameter")]
    int? Count
);
```

**Type Mapping:**

| C# Type | JSON Schema Type |
|---------|------------------|
| `string` | `string` |
| `int`, `long`, `short` | `integer` |
| `float`, `double`, `decimal` | `number` |
| `bool` | `boolean` |
| `T[]`, `List<T>` | `array` |

**Nullable types** (`string?`, `int?`) are treated as optional parameters.

### SkillResult

Return execution results:

```csharp
// Success with output
return SkillResult.Success("Operation completed successfully.");

// Success with structured data
return SkillResult.Success(JsonSerializer.Serialize(data));

// Failure with error message
return SkillResult.Failure("File not found.");
```

### SkillContext

Contains execution context (currently includes Arguments as JSON string).

## Best Practices

### 1. Validate Input Early

```csharp
protected override Task<SkillResult> ExecuteAsync(MyArgs args, CancellationToken ct)
{
    if (string.IsNullOrEmpty(args.Path))
    {
        return Task.FromResult(SkillResult.Failure("Path is required."));
    }

    // Continue with execution...
}
```

### 2. Use Descriptive Names

```csharp
public override string Name => "read_file";  // snake_case, verb + noun
public override string Description => "Read the contents of a file at the specified path.";
```

### 3. Handle Cancellation

```csharp
protected override async Task<SkillResult> ExecuteAsync(MyArgs args, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    var content = await File.ReadAllTextAsync(args.Path, ct);
    return SkillResult.Success(content);
}
```

### 4. Provide Clear Error Messages

```csharp
if (!File.Exists(args.Path))
{
    return SkillResult.Failure($"File not found: {args.Path}");
}
```

## Example: FileSystem Skills

See [OpenClaw.Skills.FileSystem](../skills/OpenClaw.Skills.FileSystem/) for reference implementations:

- **ReadFileSkill** - Read file contents
- **WriteFileSkill** - Write content to file
- **ListDirectorySkill** - List directory contents

## Distribution

Package your Skills as NuGet:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>OpenClaw.Skills.MySkills</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OpenClaw.Contracts" Version="1.0.0" />
  </ItemGroup>
</Project>
```

Users install via:

```bash
dotnet add package OpenClaw.Skills.MySkills
```

---

## Related Resources

- [Introduction](./introduction.md)

import Revision from '@site/src/components/Revision';

<Revision date="Feb-15, 2026" version="v1.0.0" />
