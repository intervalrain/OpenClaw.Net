---
title: 認證與授權指南
description: JWT 認證、Role/Permission 授權與 Policy 機制的實作指南
keywords: [Authentication, Authorization, JWT, Role, Permission, Policy]
sidebar_position: 7
---

# 認證與授權指南

> 學習如何實作 JWT 認證與多層次授權機制

## 概觀

Weda Template 提供完整的認證與授權機制，包含：
- JWT Bearer Token 認證
- Role-Based Access Control (RBAC)
- Permission-Based Authorization
- Policy-Based Authorization

```
src/
├── Weda.Core/Application/Security/
│   ├── AuthorizeAttribute.cs         # 授權 Attribute
│   ├── IAuthorizationService.cs      # 授權服務介面
│   ├── IJwtTokenGenerator.cs         # Token 產生器介面
│   └── CurrentUserProvider/
│       ├── CurrentUser.cs            # 當前使用者模型
│       └── ICurrentUserProvider.cs   # 當前使用者提供者介面
├── OpenClaw.Infrastructure/Security/
│   ├── JwtSettings.cs                # JWT 設定
│   ├── JwtTokenGenerator.cs          # Token 產生器實作
│   ├── AuthenticationOptions.cs      # 認證開關設定
│   ├── AuthorizationService.cs       # 授權服務實作
│   └── CurrentUserProvider/
│       └── CurrentUserProvider.cs    # 當前使用者提供者實作
└── OpenClaw.Api/Auth/
    └── Controllers/AuthController.cs # 登入 API
```

---

## 1. 設定方式

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

### 1.2 停用認證（開發用）

```json
{
  "Authentication": {
    "Enabled": false
  }
}
```

> **注意**：生產環境請務必啟用認證。

---

## 2. JWT Token 機制

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

### 2.2 Token 產生器

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

        // 加入 Role Claims
        roles.ForEach(role =>
            claims.Add(new(ClaimTypes.Role, role)));

        // 加入 Permission Claims
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

### 2.3 Token 結構

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

## 3. 登入流程

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
        // 1. 查詢使用者
        var user = await userRepository.GetByEmailAsync(
            request.Email, cancellationToken);

        if (user is null)
            return UserErrors.InvalidCredentials;

        // 2. 驗證密碼
        if (!passwordHasher.VerifyPassword(
            request.Password, user.PasswordHash.Value))
            return UserErrors.InvalidCredentials;

        // 3. 檢查帳號狀態
        if (user.Status != UserStatus.Active)
            return UserErrors.AccountNotActive;

        // 4. 記錄登入
        user.RecordLogin();
        await userRepository.UpdateAsync(user, cancellationToken);

        // 5. 產生 Token
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

## 4. 授權機制

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

### 4.2 使用方式

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

// 多重授權（AND 邏輯）
[Authorize(Roles = "Admin", Permissions = "employees:delete")]
public record ForceDeleteEmployeeCommand(int Id) : ...;

// 多重授權（OR 邏輯）
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
        // 取得所有 AuthorizeAttribute
        var authorizationAttributes = request.GetType()
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        if (authorizationAttributes.Count == 0)
            return await next(request, cancellationToken);

        // 收集所需的 Permissions、Roles、Policies
        var requiredPermissions = authorizationAttributes
            .SelectMany(a => a.Permissions?.Split(',') ?? [])
            .ToList();

        var requiredRoles = authorizationAttributes
            .SelectMany(a => a.Roles?.Split(',') ?? [])
            .ToList();

        var requiredPolicies = authorizationAttributes
            .SelectMany(a => a.Policies?.Split(',') ?? [])
            .ToList();

        // 執行授權檢查
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

## 5. CurrentUser 模式

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

### 5.3 在 Handler 中使用

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

## 6. Policy 機制

### 6.1 定義 Policy

```csharp
public static class Policy
{
    public const string AdminOrAbove = nameof(AdminOrAbove);
    public const string SuperAdminOnly = nameof(SuperAdminOnly);
    public const string SelfOrAdmin = nameof(SelfOrAdmin);
}
```

### 6.2 註冊 Policy

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy(Policy.AdminOrAbove, policy =>
        policy.RequireRole(Role.Admin, Role.SuperAdmin))
    .AddPolicy(Policy.SuperAdminOnly, policy =>
        policy.RequireRole(Role.SuperAdmin));
```

### 6.3 自訂 PolicyEnforcer

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
        // Admin 直接通過
        if (currentUser.Roles.Contains(Role.Admin))
            return Result.Success;

        // 檢查是否為本人
        if (request is IUserRequest userRequest &&
            userRequest.UserId == currentUser.Id)
            return Result.Success;

        return Error.Forbidden(
            description: "Access denied: not self or admin");
    }
}
```

---

## 7. API 測試

### 7.1 登入取得 Token

```bash
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!"
  }'
```

回應：
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

### 7.2 使用 Token 存取受保護資源

```bash
curl http://localhost:5001/api/v1/employees \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### 7.3 Swagger UI 認證

1. 點擊右上角「Authorize」按鈕
2. 輸入 `Bearer <your-token>`
3. 點擊「Authorize」確認

---

## 快速參考

### 授權層級

| 層級 | 說明 | 使用時機 |
|------|------|----------|
| Role | 角色群組 | 粗粒度權限控管 |
| Permission | 特定操作權限 | 細粒度功能控管 |
| Policy | 自訂商業邏輯 | 複雜授權規則 |

### 常見 Roles

| Role | 說明 |
|------|------|
| `SuperAdmin` | 超級管理員，擁有所有權限 |
| `Admin` | 管理員 |
| `Manager` | 主管 |
| `User` | 一般使用者 |

### 常見 Permissions

| Permission | 說明 |
|------------|------|
| `employees:read` | 讀取員工資料 |
| `employees:write` | 新增/修改員工 |
| `employees:delete` | 刪除員工 |
| `users:manage` | 管理使用者 |

---

## 相關資源

- [GUIDE.md](GUIDE.md) - 學習指南總覽
- [02-application-layer.md](02-application-layer.md) - Application Layer（Pipeline Behavior）
- [03-infrastructure-layer.md](03-infrastructure-layer.md) - Infrastructure Layer（Security 實作）
- [JWT.io](https://jwt.io/) - JWT 除錯工具
