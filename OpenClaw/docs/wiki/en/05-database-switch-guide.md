---
title: Database Provider Switch Guide
description: How to switch between different database providers in Weda Template
keywords: [Database, SQLite, PostgreSQL, MongoDB, InMemory, Entity Framework]
sidebar_position: 6
---

# Database Provider Switch Guide

> Learn how to switch between different database providers

## Overview

Weda Template supports multiple database providers, allowing you to switch between them through configuration without modifying code.

```
src/OpenClaw.Infrastructure/
├── Persistence/
│   ├── DatabaseOptions.cs      # Configuration class
│   └── DatabaseProvider.cs     # Provider enum
└── WedaTemplateInfrastructureModule.cs  # DI registration logic
```

---

## 1. Supported Databases

| Provider | Description | Use Case |
|----------|-------------|----------|
| `Sqlite` | Lightweight file-based database | Development, testing, small deployments |
| `PostgreSql` | Enterprise-grade relational database | Production environments |
| `MongoDb` | NoSQL document database | Flexible schema requirements |
| `InMemory` | In-memory database | Unit testing, rapid prototyping |

---

## 2. Configuration

Modify the `Database` section in `appsettings.json` or `appsettings.Development.json`.

### 2.1 SQLite (Default)

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=OpenClaw.sqlite"
  }
}
```

**Features**:
- No additional database service installation required
- Data stored in a single file
- Ideal for rapid development and testing

### 2.2 PostgreSQL

```json
{
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Port=5432;Database=WedaTemplate;Username=postgres;Password=your_password"
  }
}
```

**Features**:
- Enterprise-grade performance and reliability
- Advanced SQL feature support
- Suitable for production environments

### 2.3 MongoDB

```json
{
  "Database": {
    "Provider": "MongoDb",
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "WedaTemplate"
  }
}
```

**Features**:
- Flexible schema design
- Suitable for unstructured data
- Strong horizontal scaling capabilities

### 2.4 InMemory

```json
{
  "Database": {
    "Provider": "InMemory",
    "DatabaseName": "WedaTemplate"
  }
}
```

**Features**:
- Fastest access speed
- Data is lost when application restarts
- Ideal for unit testing

---

## 3. Implementation Details

### 3.1 DatabaseOptions

```csharp
public class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
    public string ConnectionString { get; set; } = "Data Source=OpenClaw.sqlite";
    public string DatabaseName { get; set; } = "WedaTemplate";
}
```

### 3.2 DatabaseProvider Enum

```csharp
public enum DatabaseProvider
{
    Sqlite,
    PostgreSql,
    MongoDb,
    InMemory
}
```

### 3.3 DI Registration Logic

```csharp
private static IServiceCollection AddDatabase(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var options = configuration
        .GetSection(DatabaseOptions.SectionName)
        .Get<DatabaseOptions>() ?? new DatabaseOptions();

    services.AddDbContext<AppDbContext>(dbOptions =>
    {
        _ = options.Provider switch
        {
            DatabaseProvider.Sqlite =>
                dbOptions.UseSqlite(options.ConnectionString),
            DatabaseProvider.PostgreSql =>
                dbOptions.UseNpgsql(options.ConnectionString),
            DatabaseProvider.MongoDb =>
                dbOptions.UseMongoDB(
                    options.ConnectionString,
                    options.DatabaseName),
            DatabaseProvider.InMemory =>
                dbOptions.UseInMemoryDatabase(options.DatabaseName),
            _ => throw new InvalidOperationException(
                $"Unsupported provider: {options.Provider}")
        };
    });

    return services;
}
```

---

## 4. Testing Steps

### 4.1 Test SQLite (Default)

```bash
cd src/OpenClaw.Api
dotnet run
```

Open http://localhost:5001 and use Swagger UI to test CRUD operations.

### 4.2 Test PostgreSQL

**Prerequisites**: Start PostgreSQL using Docker

```bash
docker run --name weda-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=WedaTemplate \
  -p 5432:5432 \
  -d postgres:16
```

After modifying `appsettings.Development.json`, run:

```bash
dotnet run
```

### 4.3 Test MongoDB

**Prerequisites**: Start MongoDB using Docker

```bash
docker run --name weda-mongo \
  -p 27017:27017 \
  -d mongo:7
```

After modifying `appsettings.Development.json`, run:

```bash
dotnet run
```

### 4.4 Test InMemory

After modifying `appsettings.Development.json`, run:

```bash
dotnet run
```

> **Note**: InMemory database loses all data when the application restarts.

---

## 5. Validation Tests

Execute the following operations for each provider to validate functionality:

### 5.1 Create Employee

```bash
curl -X POST http://localhost:5001/api/v1/employees \
  -H "Content-Type: application/json" \
  -d '{
    "name": "John Doe",
    "email": "john@example.com",
    "department": "Engineering",
    "position": "Developer",
    "hireDate": "2024-01-15"
  }'
```

**Expected response**: `201 Created` with employee data.

### 5.2 Get All Employees

```bash
curl http://localhost:5001/api/v1/employees
```

**Expected response**: Contains the newly created employee.

### 5.3 Verify After Restart (Except InMemory)

```bash
# Stop the server (Ctrl+C)
dotnet run

# Query again
curl http://localhost:5001/api/v1/employees
```

**Expected response**: Data persists (SQLite, PostgreSQL, MongoDB).

---

## 6. Cleanup Test Environment

```bash
# Stop and remove Docker containers
docker stop weda-postgres weda-mongo
docker rm weda-postgres weda-mongo

# Delete SQLite file
rm src/OpenClaw.Api/OpenClaw.sqlite
```

---

## 7. Important Notes

### 7.1 MongoDB Limitations

EF Core support for MongoDB is relatively new, and some advanced queries may not be supported. Verify required functionality before use.

### 7.2 Migration Strategy

Currently uses `EnsureDatabaseCreated` for automatic table creation. For production environments, use EF Core Migrations:

```bash
# Create migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update
```

### 7.3 Connection String Security

For production environments, use these methods to manage sensitive information:

- **User Secrets** (Development)
  ```bash
  dotnet user-secrets set "Database:ConnectionString" "your-connection-string"
  ```

- **Environment Variables** (Production)
  ```bash
  export Database__ConnectionString="your-connection-string"
  ```

---

## Quick Reference

### Provider Comparison

| Feature | SQLite | PostgreSQL | MongoDB | InMemory |
|---------|--------|------------|---------|----------|
| Persistence | ✅ | ✅ | ✅ | ❌ |
| Transaction Support | ✅ | ✅ | Partial | ✅ |
| Horizontal Scaling | ❌ | Limited | ✅ | ❌ |
| Setup Complexity | Low | Medium | Medium | Lowest |
| Best For | Dev/Small | Production | Flexible Schema | Testing |

### FAQ

**Q: Do I need to modify repositories when switching providers?**

A: No. Repositories work through EF Core's abstraction layer, making provider switching transparent to upper layers.

**Q: How do I use InMemory database in tests?**

A: Override the configuration in your test project:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TestDb"));
```

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer Guide
- [Entity Framework Core - Database Providers](https://learn.microsoft.com/ef/core/providers/)
