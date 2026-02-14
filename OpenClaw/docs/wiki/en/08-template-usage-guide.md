---
title: dotnet new Template Usage Guide
description: How to install, use, and customize Weda Template
keywords: [dotnet new, Template, Installation, Configuration]
sidebar_position: 9
---

# dotnet new Template Usage Guide

> Learn how to install and use Weda Clean Architecture Template

## Overview

Weda Template is a `dotnet new` project template that allows you to quickly create projects following Clean Architecture through the command line.

```
.template.config/
└── template.json    # Template configuration file
```

---

## 1. Installing the Template

### 1.1 Install from Local

```bash
# Navigate to template root directory
cd WedaTemplate

# Install template
dotnet new install .

# Verify installation
dotnet new list | grep weda
```

### 1.2 Uninstall

```bash
dotnet new uninstall WedaTemplate
```

---

## 2. Creating a New Project

### 2.1 Basic Usage

```bash
# Create new project (with default settings)
dotnet new weda -n MyProject

# Specify output directory
dotnet new weda -n MyProject -o ./src/MyProject
```

### 2.2 View Available Parameters

```bash
dotnet new weda --help
```

---

## 3. Template Parameters

### 3.1 Database Option (`--db`)

Choose the database provider to use:

| Value | Description | Default |
|-------|-------------|---------|
| `sqlite` | SQLite lightweight database | ✅ |
| `postgres` | PostgreSQL |  |
| `mongo` | MongoDB |  |
| `none` | InMemory (no external database) |  |

```bash
# Use PostgreSQL
dotnet new weda -n MyProject --db postgres

# Use MongoDB
dotnet new weda -n MyProject --db mongo

# Use InMemory
dotnet new weda -n MyProject --db none
```

### 3.2 NATS Service Name (`--Nats`)

Set the NATS service name:

```bash
dotnet new weda -n MyProject --Nats my-service
```

Default: `openclaw`

### 3.3 Test Projects (`--test`)

Whether to include test projects:

```bash
# Exclude test projects
dotnet new weda -n MyProject --test false
```

Default: `true`

### 3.4 Wiki Documentation (`--wiki`)

Whether to include Wiki documentation and generator:

```bash
# Exclude Wiki
dotnet new weda -n MyProject --wiki false
```

Default: `true`

### 3.5 Sample Module (`--sample`)

Whether to include the Employee sample module:

```bash
# Exclude sample module
dotnet new weda -n MyProject --sample false
```

Default: `true`

---

## 4. Common Combinations

### 4.1 Minimal Project (Production)

```bash
dotnet new weda -n MyProject \
  --db postgres \
  --sample false \
  --wiki false
```

### 4.2 Development/Learning

```bash
dotnet new weda -n MyProject \
  --db sqlite \
  --sample true \
  --wiki true \
  --test true
```

### 4.3 Rapid Prototype

```bash
dotnet new weda -n MyProject \
  --db none \
  --sample false \
  --test false
```

---

## 5. Template Configuration

### 5.1 template.json Structure

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Rain Hu",
  "classifications": ["Web", "API", "Clean Architecture", "DDD", "CQRS"],
  "identity": "OpenClaw",
  "name": "Weda Clean Architecture Template",
  "shortName": "weda",
  "description": "A clean architecture template with DDD, CQRS, NATS EventController, and Entity Framework Core.",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "OpenClaw",
  "preferNameDirectory": true
}
```

### 5.2 Parameter Definition

```json
{
  "symbols": {
    "db": {
      "type": "parameter",
      "description": "Database provider to use.",
      "datatype": "choice",
      "choices": [
        { "choice": "sqlite", "description": "SQLite (default)" },
        { "choice": "postgres", "description": "PostgreSQL" },
        { "choice": "mongo", "description": "MongoDB" },
        { "choice": "none", "description": "InMemory" }
      ],
      "defaultValue": "sqlite"
    }
  }
}
```

### 5.3 Conditional File Exclusion

```json
{
  "sources": [
    {
      "modifiers": [
        {
          "condition": "(!test)",
          "exclude": ["tests/**/*"]
        },
        {
          "condition": "(!sample)",
          "exclude": [
            "src/OpenClaw.Api/Employees/**/*",
            "src/OpenClaw.Domain/Employees/**/*",
            "src/OpenClaw.Application/Employees/**/*",
            "src/OpenClaw.Contracts/Employees/**/*",
            "src/OpenClaw.Infrastructure/Employees/**/*"
          ]
        }
      ]
    }
  ]
}
```

### 5.4 Conditional Compilation Symbols

Template uses `#if` preprocessor directives:

```csharp
services.AddDbContext<AppDbContext>(dbOptions =>
{
#if sqlite
    dbOptions.UseSqlite(options.ConnectionString);
#elif postgres
    dbOptions.UseNpgsql(options.ConnectionString);
#elif mongo
    dbOptions.UseMongoDB(options.ConnectionString, options.DatabaseName);
#elif nodb
    dbOptions.UseInMemoryDatabase(options.DatabaseName);
#endif
});
```

---

## 6. Post-Creation Steps

### 6.1 Restore Packages

```bash
cd MyProject
dotnet restore
```

### 6.2 Build Project

```bash
dotnet build
```

### 6.3 Run Application

```bash
cd src/MyProject.Api
dotnet run
```

### 6.4 Run Tests

```bash
dotnet test
```

---

## 7. Customizing the Template

### 7.1 Adding Parameters

Add to the `symbols` section in `template.json`:

```json
{
  "symbols": {
    "useRedis": {
      "type": "parameter",
      "description": "Include Redis caching support.",
      "datatype": "bool",
      "defaultValue": "false"
    }
  }
}
```

### 7.2 Adding Conditional Code

```csharp
#if useRedis
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});
#endif
```

### 7.3 Adding Conditional Files

```json
{
  "sources": [
    {
      "modifiers": [
        {
          "condition": "(!useRedis)",
          "exclude": ["src/**/Redis/**/*"]
        }
      ]
    }
  ]
}
```

---

## Quick Reference

### Complete Parameter List

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--db` | choice | `sqlite` | Database provider |
| `--Nats` | string | `openclaw` | NATS service name |
| `--test` | bool | `true` | Include test projects |
| `--wiki` | bool | `true` | Include Wiki documentation |
| `--sample` | bool | `true` | Include sample module |

### Generated Project Structure

```
MyProject/
├── src/
│   ├── MyProject.Api/           # API Layer
│   ├── MyProject.Application/   # Application Layer
│   ├── MyProject.Contracts/     # DTOs & Requests
│   ├── MyProject.Domain/        # Domain Layer
│   ├── MyProject.Infrastructure/# Infrastructure Layer
│   └── Weda.Core/               # Shared core library
├── tests/                       # (--test true)
│   ├── MyProject.Domain.UnitTests/
│   ├── MyProject.Application.UnitTests/
│   ├── MyProject.Infrastructure.UnitTests/
│   ├── MyProject.Api.IntegrationTests/
│   └── MyProject.TestCommon/
├── docs/                        # (--wiki true)
│   └── wiki/
└── tools/                       # (--wiki true)
    └── WikiGenerator/
```

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [05-database-switch-guide.md](05-database-switch-guide.md) - Database Switch Guide
- [dotnet new Templates Documentation](https://learn.microsoft.com/dotnet/core/tools/custom-templates)
