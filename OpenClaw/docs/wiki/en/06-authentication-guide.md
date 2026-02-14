---
title: Authentication & Authorization Guide
description: Implementation guide for JWT authentication, Role/Permission authorization, and Policy mechanisms
keywords: [Authentication, Authorization, JWT, Role, Permission, Policy]
sidebar_position: 7
---

# Authentication & Authorization Guide

> Learn how to implement JWT authentication and multi-level authorization

## Overview

Weda Template provides a complete authentication and authorization mechanism, including:
- JWT Bearer Token authentication
- Role-Based Access Control (RBAC)
- Permission-Based Authorization
- Policy-Based Authorization

```
src/
├── Weda.Core/Application/Security/
│   ├── AuthorizeAttribute.cs         # Authorization attribute
│   ├── IAuthorizationService.cs      # Authorization service interface
│   ├── IJwtTokenGenerator.cs         # Token generator interface
│   └── CurrentUserProvider/
│       ├── CurrentUser.cs            # Current user model
│       └── ICurrentUserProvider.cs   # Current user provider interface
├── OpenClaw.Infrastructure/Security/
│   ├── JwtSettings.cs                # JWT settings
│   ├── JwtTokenGenerator.cs          # Token generator implementation
│   ├── AuthenticationOptions.cs      # Authentication toggle settings
│   ├── AuthorizationService.cs       # Authorization service implementation
│   └── CurrentUserProvider/
│       └── CurrentUserProvider.cs    # Current user provider implementation
└── OpenClaw.Api/Auth/
    └── Controllers/AuthController.cs # Login API
```

---

## 1. Configuration

### 1.1 appsettings.json

```json
{
  "Authentication": {
    "Enabled": true
  },
  "JwtSettings": {
    "Secret": "your-256-bit-secret-key-here-min-32-chars",
    "Issuer": "WedaTemplate",
    "Audience": "WedaTemplateUsers",
    "TokenExpirationInMinutes": 60
  }
}
```

### 1.2 Disable Authentication (Development)

```json
{
  "Authentication": {
    "Enabled": false
  }
}
```

> **Note**: Always enable authentication in production environments.

---

## 2. JWT Token Mechanism

### 2.1 JwtSettings

```csharp
public sealed class JwtSettings
{
    public const string Section = "JwtSettings";

    public string Secret { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int TokenExpirationInMinutes { get; set; }
}
```

### 2.2 Token Generator

```csharp
public class JwtTokenGenerator(IOptions<JwtSettings> jwtOptions) : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    public string GenerateToken(
        Guid id,
        string name,
        string email,
        List<string> permissions,
        List<string> roles)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Name, name),
            new(JwtRegisteredClaimNames.Email, email),
            new("id", id.ToString()),
        };

        // Add Role Claims
        roles.ForEach(role =>
            claims.Add(new(ClaimTypes.Role, role)));

        // Add Permission Claims
        permissions.ForEach(permission =>
            claims.Add(new("permissions", permission)));

        var token = new JwtSecurityToken(
            _jwtSettings.Issuer,
            _jwtSettings.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(
                _jwtSettings.TokenExpirationInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### 2.3 Token Structure

```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "name": "John Doe",
    "email": "john@example.com",
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "role": ["Admin", "User"],
    "permissions": ["employees:read", "employees:write"],
    "exp": 1234567890,
    "iss": "WedaTemplate",
    "aud": "WedaTemplateUsers"
  }
}
```

---

## 3. Login Flow

### 3.1 LoginCommand

```csharp
public record LoginCommand(string Email, string Password)
    : IRequest<ErrorOr<AuthResponse>>;
```

### 3.2 LoginCommandHandler

```csharp
public class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator)
    : IRequestHandler<LoginCommand, ErrorOr<AuthResponse>>
{
    public async ValueTask<ErrorOr<AuthResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find user
        var user = await userRepository.GetByEmailAsync(
            request.Email, cancellationToken);

        if (user is null)
            return UserErrors.InvalidCredentials;

        // 2. Verify password
        if (!passwordHasher.VerifyPassword(
            request.Password, user.PasswordHash.Value))
            return UserErrors.InvalidCredentials;

        // 3. Check account status
        if (user.Status != UserStatus.Active)
            return UserErrors.AccountNotActive;

        // 4. Record login
        user.RecordLogin();
        await userRepository.UpdateAsync(user, cancellationToken);

        // 5. Generate token
        var token = jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Name,
            user.Email.Value,
            user.Permissions.ToList(),
            user.Roles.ToList());

        return new AuthResponse(
            Token: token,
            Id: user.Id,
            Name: user.Name,
            Email: user.Email.Value,
            Permissions: user.Permissions,
            Roles: user.Roles);
    }
}
```

### 3.3 AuthController

```csharp
[AllowAnonymous]
[ApiVersion("1.0")]
public class AuthController(ISender _mediator) : ApiController
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command);

        return result.Match(Ok, Problem);
    }
}
```

---

## 4. Authorization Mechanism

### 4.1 AuthorizeAttribute

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeAttribute : Attribute
{
    public string? Permissions { get; set; }
    public string? Roles { get; set; }
    public string? Policies { get; set; }
}
```

### 4.2 Usage

```csharp
// Role-Based
[Authorize(Roles = "Admin")]
public record DeleteEmployeeCommand(int Id)
    : IRequest<ErrorOr<Deleted>>, IAuthorizeableRequest<ErrorOr<Deleted>>;

// Permission-Based
[Authorize(Permissions = "employees:write")]
public record UpdateEmployeeCommand(...)
    : IRequest<ErrorOr<EmployeeResponse>>, IAuthorizeableRequest<ErrorOr<EmployeeResponse>>;

// Policy-Based
[Authorize(Policies = "SelfOrAdmin")]
public record GetUserQuery(Guid UserId)
    : IRequest<ErrorOr<UserResponse>>, IAuthorizeableRequest<ErrorOr<UserResponse>>;

// Multiple Authorization (AND logic)
[Authorize(Roles = "Admin", Permissions = "employees:delete")]
public record ForceDeleteEmployeeCommand(int Id) : ...;

// Multiple Authorization (OR logic)
[Authorize(Roles = "Admin")]
[Authorize(Roles = "Manager")]
public record ApproveCommand(int Id) : ...;
```

### 4.3 AuthorizationBehavior

```csharp
public class AuthorizationBehavior<TRequest, TResponse>(
    IAuthorizationService _authorizationService)
    : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IAuthorizeableRequest<TResponse>
        where TResponse : IErrorOr
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Get all AuthorizeAttributes
        var authorizationAttributes = request.GetType()
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        if (authorizationAttributes.Count == 0)
            return await next(request, cancellationToken);

        // Collect required Permissions, Roles, Policies
        var requiredPermissions = authorizationAttributes
            .SelectMany(a => a.Permissions?.Split(',') ?? [])
            .ToList();

        var requiredRoles = authorizationAttributes
            .SelectMany(a => a.Roles?.Split(',') ?? [])
            .ToList();

        var requiredPolicies = authorizationAttributes
            .SelectMany(a => a.Policies?.Split(',') ?? [])
            .ToList();

        // Execute authorization check
        var authorizationResult = _authorizationService
            .AuthorizeCurrentUser(
                request,
                requiredRoles,
                requiredPermissions,
                requiredPolicies);

        return authorizationResult.IsError
            ? (dynamic)authorizationResult.Errors
            : await next(request, cancellationToken);
    }
}
```

---

## 5. CurrentUser Pattern

### 5.1 CurrentUser Model

```csharp
public record CurrentUser(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Roles);
```

### 5.2 ICurrentUserProvider

```csharp
public interface ICurrentUserProvider
{
    CurrentUser GetCurrentUser();
}
```

### 5.3 Using in Handlers

```csharp
public class GetCurrentUserQueryHandler(
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetCurrentUserQuery, ErrorOr<UserResponse>>
{
    public ValueTask<ErrorOr<UserResponse>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserProvider.GetCurrentUser();

        return ValueTask.FromResult<ErrorOr<UserResponse>>(
            new UserResponse(
                currentUser.Id,
                currentUser.Name,
                currentUser.Email,
                currentUser.Roles,
                currentUser.Permissions));
    }
}
```

---

## 6. Policy Mechanism

### 6.1 Define Policies

```csharp
public static class Policy
{
    public const string AdminOrAbove = nameof(AdminOrAbove);
    public const string SuperAdminOnly = nameof(SuperAdminOnly);
    public const string SelfOrAdmin = nameof(SelfOrAdmin);
}
```

### 6.2 Register Policies

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy(Policy.AdminOrAbove, policy =>
        policy.RequireRole(Role.Admin, Role.SuperAdmin))
    .AddPolicy(Policy.SuperAdminOnly, policy =>
        policy.RequireRole(Role.SuperAdmin));
```

### 6.3 Custom PolicyEnforcer

```csharp
public class PolicyEnforcer : IPolicyEnforcer
{
    public ErrorOr<Success> Enforce<T>(
        string policy,
        CurrentUser currentUser,
        T request)
    {
        return policy switch
        {
            Policy.SelfOrAdmin => EnforceSelfOrAdmin(currentUser, request),
            _ => Error.Validation(description: $"Unknown policy: {policy}")
        };
    }

    private ErrorOr<Success> EnforceSelfOrAdmin<T>(
        CurrentUser currentUser,
        T request)
    {
        // Admin passes directly
        if (currentUser.Roles.Contains(Role.Admin))
            return Result.Success;

        // Check if self
        if (request is IUserRequest userRequest &&
            userRequest.UserId == currentUser.Id)
            return Result.Success;

        return Error.Forbidden(
            description: "Access denied: not self or admin");
    }
}
```

---

## 7. API Testing

### 7.1 Login to Get Token

```bash
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!"
  }'
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Admin",
  "email": "admin@example.com",
  "permissions": ["employees:read", "employees:write"],
  "roles": ["Admin"]
}
```

### 7.2 Access Protected Resources with Token

```bash
curl http://localhost:5001/api/v1/employees \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### 7.3 Swagger UI Authentication

1. Click the "Authorize" button in the top right
2. Enter `Bearer <your-token>`
3. Click "Authorize" to confirm

---

## Quick Reference

### Authorization Levels

| Level | Description | Use Case |
|-------|-------------|----------|
| Role | Role groups | Coarse-grained access control |
| Permission | Specific operation permissions | Fine-grained feature control |
| Policy | Custom business logic | Complex authorization rules |

### Common Roles

| Role | Description |
|------|-------------|
| `SuperAdmin` | Super administrator with all permissions |
| `Admin` | Administrator |
| `Manager` | Manager |
| `User` | Regular user |

### Common Permissions

| Permission | Description |
|------------|-------------|
| `employees:read` | Read employee data |
| `employees:write` | Create/update employees |
| `employees:delete` | Delete employees |
| `users:manage` | Manage users |

---

## Related Resources

- [GUIDE.md](GUIDE.md) - Learning Guide Overview
- [02-application-layer.md](02-application-layer.md) - Application Layer (Pipeline Behavior)
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer (Security Implementation)
- [JWT.io](https://jwt.io/) - JWT Debugging Tool
