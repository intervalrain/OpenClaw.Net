---
title: WEDA Clean Architecture Template 學習指南
description: 從零開始到專精的完整學習指南，適用於內部團隊培訓
keywords: [Clean Architecture, DDD, CQRS, Mediator, .NET]
sidebar_position: 1
---

# WEDA Clean Architecture Template - 學習指南

> 從零開始到專精：內部團隊培訓的完整指南

## Part 1: 基礎概念

### 1. Clean Architecture 概觀
- Uncle Bob 的 Clean Architecture 原則
- Dependency Rule：依賴方向指向內層
- 獨立於 Framework、UI、Database 及外部代理
- 以可測試性為設計核心

### 2. 專案結構與 Layer 依賴關係
- Solution 組織架構：Domain → Application → Infrastructure → Api
- Project Reference 與依賴方向
- Contracts Project：Layer 間共用的 DTO
- 為何分層對維護性至關重要

### 3. Domain-Driven Design (DDD) 基礎
- Ubiquitous Language
- Bounded Context
- Entity vs Value Object
- Aggregate 與 Aggregate Root

---

## Part 2: Domain Layer

### 4. Entity 與 Aggregate Root
- 具有 Id 與 Domain Event 的 Entity Base Class
- Aggregate Root 作為一致性邊界
- Encapsulation：Private Setter、受控的狀態變更
- 範例：`Employee` 作為 Aggregate Root

### 5. Value Object
- Immutability 與 Value Equality
- 自我驗證的物件
- 何時使用 Value Object vs Entity
- 範例：`Email`、`Money`、`Address`

### 6. Factory Method Pattern
- 為何 Constructor 應該是 Private
- 回傳 `ErrorOr<T>` 的 `Create()` 方法
- 建立時進行驗證
- Domain Invariant 的強制執行

### 7. Domain Error (ErrorOr Pattern)
- Railway-Oriented Programming
- 使用 `ErrorOr<T>` 進行明確的錯誤處理
- 在 `*Errors` Class 中定義 Static Error
- Error 類型：Validation、NotFound、Conflict、Unauthorized
- 詳見 [09-error-handling-guide.md](09-error-handling-guide.md)

### 8. Domain Event
- 什麼是 Domain Event
- `IDomainEvent` Interface
- 在 Aggregate Root 中 Raise Event
- Event Collection 與 Publishing Pattern
- 詳見 [11-domain-events-guide.md](11-domain-events-guide.md)

---

## Part 3: Application Layer

### 9. CQRS Pattern (Command Query Responsibility Segregation)
- Command：改變狀態的寫入操作
- Query：回傳資料的讀取操作
- 為何要分離 Command 與 Query
- 資料夾結構：`Commands/` 與 `Queries/`

### 10. Mediator 與 Request/Handler Pattern
- `IRequest<T>` 與 `IRequestHandler<TRequest, TResponse>`
- 解耦 Sender 與 Handler
- 每個 Request 對應一個 Handler
- 透過 Assembly Scanning 進行註冊

### 11. Repository Interface (IRepository<T>)
- Generic Repository Pattern
- `IRepository<T>` Base Interface
- 特化的 Interface：`IEmployeeRepository`
- 為何 Interface 應該放在 Application Layer

### 12. Pipeline Behavior (Validation, Authorization)
- Mediator Pipeline 概念
- `IPipelineBehavior<TRequest, TResponse>`
- Cross-Cutting Concern：Logging、Validation、Authorization
- 執行順序與串接

### 13. FluentValidation
- 使用 `AbstractValidator<T>` 進行 Request 驗證
- Validation Rule 與 Error Message
- 與 Mediator Pipeline 整合
- 自訂 Validator
- 詳見 [10-validation-guide.md](10-validation-guide.md)

---

## Part 4: Infrastructure Layer

### 14. Generic Repository 實作
- `GenericRepository<T>` 實作 `IRepository<T>`
- Entity Framework Core 整合
- CRUD 操作實作
- Unit of Work Pattern（選用）

### 15. Entity Framework Core 與 DbContext
- `AppDbContext` 設定
- 在 SaveChanges 時發布 Domain Event
- Connection String 管理
- Database Provider：SQLite、PostgreSQL、MongoDB

### 16. Database Configuration (EF Configuration)
- `IEntityTypeConfiguration<T>`
- 使用 Fluent API 進行 Mapping
- Value Object 的 Value Converter
- Index 與 Constraint

### 17. Dependency Injection Module Pattern
- `*Module.cs` 命名慣例
- `IServiceCollection` 的 Extension Method
- Service Lifetime：Singleton、Scoped、Transient
- 使用 `IOptions<T>` 進行 Configuration Binding

---

## Part 5: API Layer

### 18. Controller 與 Routing
- `[ApiController]` Attribute
- Route 慣例：`[Route("api/[controller]")]`
- Action Method 與 HTTP Verb
- 注入 Mediator `ISender`

### 19. Contract (DTO) 與 Mapping
- Request 與 Response DTO
- 為何 Contract 要與 Domain 分離
- 手動 Mapping vs Mapperly
- API Versioning 考量

### 20. Error Handling 與 ProblemDetails
- Global Exception Handling
- `ProblemDetails` RFC 7807
- 將 `ErrorOr` 對應到 HTTP Response
- 一致的錯誤格式

### 21. Authentication 與 Authorization
- JWT Bearer Authentication
- `[Authorize]` Attribute
- Policy-Based Authorization
- Current User Provider Pattern
- 詳見 [06-authentication-guide.md](06-authentication-guide.md)

---

## Part 6: Testing

### 22. Unit Testing (Domain, Application)
- 隔離測試 Domain Logic
- 使用 NSubstitute 進行 Mock
- 測試 Command/Query Handler
- 使用 FluentAssertions 提升可讀性

### 23. Integration Testing (WebApplicationFactory)
- `WebApplicationFactory<T>` 設定
- 使用 In-Memory Database 進行測試
- HTTP Client 測試
- Test Fixture 與 Collection

### 24. Test Utility 與 Factory
- `TestCommon` 共用專案
- Object Mother 與 Builder
- `CurrentUserFactory` 用於 Auth 測試
- 可重複使用的 Test Helper
- 詳見 [07-testing-guide.md](07-testing-guide.md)

---

## Part 7: 進階主題

### 25. ServiceFramework 整合 (NATS, JetStream)
- EdgeSync.ServiceFramework Package
- 用於 Request/Response 的 NatsService
- 用於 Event Handling 的 BaseEventHandler
- 用於 Event Publishing 的 JetStreamClient

### 26. 多資料庫支援
- Database Abstraction 策略
- 開發用的 InMemory Repository
- 生產環境用的 PostgreSQL
- MongoDB 作為替代方案
- 詳見 [05-database-switch-guide.md](05-database-switch-guide.md)

### 27. DevContainer 與 Docker Compose
- `.devcontainer/` 設定
- `docker-compose.yml` 用於相依服務
- 開發環境建置
- 團隊一致的開發環境

### 28. dotnet new Template 設定
- `template.json` 結構
- Template Parameter 與 Symbol
- 條件式檔案引入
- 發布至 NuGet
- 詳見 [08-template-usage-guide.md](08-template-usage-guide.md)

---

## 快速參考

### Layer 依賴關係
```
Api → Application → Domain
 ↓         ↓
Infrastructure
     ↓
   Domain
```

### 使用的關鍵 Pattern
| Pattern | 位置 | 用途 |
|---------|------|------|
| Factory Method | Domain | 受控的 Entity 建立 |
| Repository | Application/Infrastructure | Data Access 抽象化 |
| CQRS | Application | 分離讀寫操作 |
| Mediator | Application | 解耦 Request 處理 |
| Module | All Layer | 組織 DI 註冊 |

### Error Handling 流程
```
Domain Error → ErrorOr<T> → Handler → Controller → ProblemDetails → HTTP Response
```

---

## 建議學習路徑

1. **第一週**：Part 1-2（基礎概念 + Domain）
2. **第二週**：Part 3（Application Layer）
3. **第三週**：Part 4-5（Infrastructure + API）
4. **第四週**：Part 6（Testing）
5. **第五週**：Part 7（進階主題）

---

## 相關資源

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [ErrorOr Library](https://github.com/amantinband/error-or)
- [Mediator Documentation](https://github.com/martinothamar/Mediator)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [Amantinband Clean Architecture Template](https://github.com/amantinband/clean-architecture)
