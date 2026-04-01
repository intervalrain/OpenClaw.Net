namespace Weda.Core.Infrastructure.Middleware;

/// <summary>
/// Indicates that the controller or action should skip the transaction
/// managed by <see cref="EventualConsistencyMiddleware{TDbContext}"/>.
/// Use this for endpoints that proxy requests (e.g., NATS gateway) to avoid deadlocks.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SkipTransactionAttribute : Attribute;