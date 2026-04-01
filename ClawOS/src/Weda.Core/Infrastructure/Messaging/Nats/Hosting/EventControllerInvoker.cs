using System.Text.Json;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Weda.Core.Infrastructure.Audit;
using Weda.Core.Infrastructure.Messaging.Nats.Discovery;
using Weda.Core.Infrastructure.Messaging.Nats.Middleware;

namespace Weda.Core.Infrastructure.Messaging.Nats.Hosting;

public class EventControllerInvoker(IServiceScopeFactory scopeFactory)
{
    public async Task<object?> InvokeAsync(
        EndpointDescriptor endpoint,
        byte[]? data,
        string subject,
        NatsHeaders? headers,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // Parse subject to extract placeholder values (e.g., {id} from subject)
        var subjectValues = TemplateResolver.ParseSubject(
            endpoint.SubjectPattern,
            endpoint.ControllerType,
            subject);

        var request = DeserializeRequest(data, endpoint.RequestType);
        var controller = CreateController(scope, endpoint, subject, headers, subjectValues);

        // Extract audit context
        var traceContext = headers.GetTraceContext();

        // Build context for middleware
        var context = new EventControllerContext
        {
            Controller = controller,
            Endpoint = endpoint,
            Headers = headers,
            Subject = subject,
            AuditContext = traceContext,
            Services = scope.ServiceProvider
        };

        object? result = null;

        // Build middleware pipeline
        var middlewares = scope.ServiceProvider.GetServices<IEventControllerMiddleware>().ToList();

        async Task ExecuteHandler()
        {
            AuditContextAccessor.Current = traceContext;
            try
            {
                result = await InvokeMethodAsync(controller, endpoint, request, subjectValues, cancellationToken);
            }
            finally
            {
                AuditContextAccessor.Current = null;
            }
        }

        // Execute pipeline: middleware1 -> middleware2 -> ... -> handler
        var pipeline = middlewares
            .AsEnumerable()
            .Reverse()
            .Aggregate((Func<Task>)ExecuteHandler, (next, middleware) => () => middleware.InvokeAsync(context, next));

        await pipeline();

        return result;
    }

    private static object? DeserializeRequest(byte[]? data, Type? requestType)
    {
        if (requestType is null || data is null || data.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize(data, requestType, WedaJsonDefaults.Options);
    }

    private static EventController CreateController(
        AsyncServiceScope scope,
        EndpointDescriptor endpoint,
        string subject,
        NatsHeaders? headers,
        Dictionary<string, string> subjectValues)
    {
        var provider = scope.ServiceProvider;

        var controller = (EventController)provider.GetRequiredService(endpoint.ControllerType);
        controller.Mediator = provider.GetRequiredService<IMediator>();
        controller.Logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(endpoint.ControllerType);
        controller.Subject = subject;
        controller.Headers = headers;
        controller.SubjectValues = subjectValues;

        return controller;
    }

    private static async Task<object?> InvokeMethodAsync(
        EventController controller,
        EndpointDescriptor endpoint,
        object? request,
        Dictionary<string, string> subjectValues,
        CancellationToken cancellationToken)
    {
        var parameters = endpoint.Method.GetParameters();
        var args = BuildArguments(parameters, request, subjectValues, cancellationToken);

        var result = endpoint.Method.Invoke(controller, args);

        if (result is Task task)
        {
            await task;
            return GetTaskResult(task);
        }

        if (result is ValueTask valueTask)
        {
            await valueTask;
            return null;
        }

        return result;
    }

    private static object?[] BuildArguments(
        System.Reflection.ParameterInfo[] parameters,
        object? request,
        Dictionary<string, string> subjectValues,
        CancellationToken cancellationToken)
    {
        if (parameters.Length == 0)
        {
            return [];
        }

        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramType = param.ParameterType;
            var paramName = param.Name ?? string.Empty;

            // CancellationToken
            if (paramType == typeof(CancellationToken))
            {
                args[i] = cancellationToken;
                continue;
            }

            // Try to get value from subject placeholders (e.g., {id})
            if (subjectValues.TryGetValue(paramName, out var subjectValue))
            {
                args[i] = ConvertValue(subjectValue, paramType);
                continue;
            }

            // Complex type - use deserialized request
            if (IsComplexType(paramType))
            {
                args[i] = request;
                continue;
            }

            // Default value for value types
            args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
        }

        return args;
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
        {
            return value;
        }

        if (underlyingType == typeof(int))
        {
            return int.TryParse(value, out var intVal) ? intVal : 0;
        }

        if (underlyingType == typeof(long))
        {
            return long.TryParse(value, out var longVal) ? longVal : 0L;
        }

        if (underlyingType == typeof(Guid))
        {
            return Guid.TryParse(value, out var guidVal) ? guidVal : Guid.Empty;
        }

        if (underlyingType == typeof(bool))
        {
            return bool.TryParse(value, out var boolVal) && boolVal;
        }

        if (underlyingType == typeof(double))
        {
            return double.TryParse(value, out var doubleVal) ? doubleVal : 0.0;
        }

        if (underlyingType == typeof(decimal))
        {
            return decimal.TryParse(value, out var decimalVal) ? decimalVal : 0m;
        }

        // Try general Convert
        try
        {
            return Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }

    private static bool IsComplexType(Type type)
    {
        // Check if it's a complex type (class or struct, but not string or primitives)
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            return IsComplexType(underlyingType);
        }

        return type.IsClass || (type.IsValueType && !type.IsPrimitive);
    }

    private static object? GetTaskResult(Task task)
    {
        var taskType = task.GetType();
        if (!taskType.IsGenericType)
        {
            return null;
        }

        var resultProperty = taskType.GetProperty("Result");
        return resultProperty?.GetValue(task);
    }
}