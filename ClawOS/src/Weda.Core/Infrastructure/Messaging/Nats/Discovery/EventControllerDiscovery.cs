using System.Reflection;
using Weda.Core.Infrastructure.Messaging.Nats.Attributes;
using Weda.Core.Infrastructure.Messaging.Nats.Enums;

namespace Weda.Core.Infrastructure.Messaging.Nats.Discovery;

public class EventControllerDiscovery(string defaultConnection = "default")
{
    private readonly string _defaultConnection = defaultConnection;

    public List<EndpointDescriptor> Endpoints { get; } = [];
    public List<EndpointDescriptor> RequestReplyEndpoints { get; } = [];
    public List<EndpointDescriptor> CorePubSubEndpoints { get; } = [];
    public List<EndpointDescriptor> JetStreamConsumeEndpoints { get; } = [];
    public List<EndpointDescriptor> JetStreamFetchEndpoints { get; } = [];

    public void DiscoverControllers(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var controllerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(EventController)));

            foreach (var controllerType in controllerTypes)
            {
                DiscoverEndpoints(controllerType);
            }
        }
    }

    private void DiscoverEndpoints(Type controllerType)
    {
        var classStreamAttr = controllerType.GetCustomAttribute<StreamAttribute>();
        var classConsumerAttr = controllerType.GetCustomAttribute<ConsumerAttribute>();
        var classConnectionAttr = controllerType.GetCustomAttribute<ConnectionAttribute>();

        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<SubjectAttribute>() is not null)
            .ToList();

        foreach (var method in methods)
        {
            var subjectAttr = method.GetCustomAttribute<SubjectAttribute>()!;
            var methodConnectionAttr = method.GetCustomAttribute<ConnectionAttribute>();

            var connectionName = methodConnectionAttr?.Name
                ?? classConnectionAttr?.Name
                ?? _defaultConnection;

            var streamTemplate = subjectAttr.Stream ?? classStreamAttr?.Template;
            var consumerTemplate = subjectAttr.Consumer ?? classConsumerAttr?.Name;

            // Resolve stream name: use template if provided, otherwise auto-generate
            var streamName = streamTemplate is not null
                ? TemplateResolver.Resolve(streamTemplate, controllerType, method.Name)
                : GenerateDefaultStreamName(controllerType);

            // Resolve consumer name: use template if provided, otherwise auto-generate
            var consumerName = consumerTemplate is not null
                ? TemplateResolver.Resolve(consumerTemplate, controllerType, method.Name)
                : GenerateDefaultConsumerName(controllerType, method.Name);

            var responseType = GetResponseType(method);
            var mode = DetermineEndpointMode(responseType, subjectAttr.DeliveryMode, subjectAttr.ConsumerMode);

            var descriptor = new EndpointDescriptor
            {
                ControllerType = controllerType,
                Method = method,
                SubjectPattern = subjectAttr.Pattern,
                Mode = mode,
                ConnectionName = connectionName,
                StreamName = streamName,
                ConsumerName = consumerName,
                RequestType = GetRequestType(method),
                ResponseType = GetResponseType(method),
            };

            Endpoints.Add(descriptor);
            switch (descriptor.Mode)
            {
                case EndpointMode.RequestReply:
                    RequestReplyEndpoints.Add(descriptor);
                    break;
                case EndpointMode.CorePubSub:
                    CorePubSubEndpoints.Add(descriptor);
                    break;
                case EndpointMode.JetStreamConsume:
                    JetStreamConsumeEndpoints.Add(descriptor);
                    break;
                case EndpointMode.JetStreamFetch:
                    JetStreamFetchEndpoints.Add(descriptor);
                    break;
                default:
                    break;
            }
        }
    }

    private EndpointMode DetermineEndpointMode(Type? responseType, DeliveryMode deliveryMode, ConsumerMode consumerMode)
    {
        if (responseType is not null)
        {
            return EndpointMode.RequestReply;
        }

        if (deliveryMode == DeliveryMode.Core)
        {
            return EndpointMode.CorePubSub;
        }

        return consumerMode == ConsumerMode.Consume ? EndpointMode.JetStreamConsume : EndpointMode.JetStreamFetch;
    }

    private static Type? GetRequestType(MethodInfo method)
    {
        var parameters = method.GetParameters();

        // Find the first complex type parameter (the body), skipping primitives and CancellationToken
        foreach (var param in parameters)
        {
            var paramType = param.ParameterType;

            // Skip CancellationToken
            if (paramType == typeof(CancellationToken))
                continue;

            // Skip primitive types (they come from subject placeholders like {id})
            if (IsPrimitiveOrSimpleType(paramType))
                continue;

            // Found a complex type - this is the request body
            return paramType;
        }

        return null;
    }

    private static bool IsPrimitiveOrSimpleType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType.IsPrimitive ||
               underlyingType == typeof(string) ||
               underlyingType == typeof(decimal) ||
               underlyingType == typeof(DateTime) ||
               underlyingType == typeof(Guid);
    }

    /// <summary>
    /// Auto-generate default stream name from controller type.
    /// Format: {controller}_v{version}_stream (e.g., "employee_v1_stream")
    /// </summary>
    private static string GenerateDefaultStreamName(Type controllerType)
    {
        var controllerName = TemplateResolver.Resolve(controllerType.Name);
        var version = TemplateResolver.GetApiVersion(controllerType) ?? "1";
        return $"{controllerName}_v{version}_stream".ToLowerInvariant();
    }

    /// <summary>
    /// Auto-generate default consumer name from controller type and method.
    /// Format: {controller}_{method}_consumer (e.g., "employee_onemployeecreated_consumer")
    /// </summary>
    private static string GenerateDefaultConsumerName(Type controllerType, string methodName)
    {
        var controllerName = TemplateResolver.Resolve(controllerType.Name);
        return $"{controllerName}_{methodName}_consumer".ToLowerInvariant();
    }

    private static Type? GetResponseType(MethodInfo method)
    {
        var returnType = method.ReturnType;

        if (returnType == typeof(Task) || returnType == typeof(ValueTask) || returnType == typeof(void))
        {
            return null;
        }

        if (returnType.IsGenericType)
        {
            var genericDef = returnType.GetGenericTypeDefinition();
            if (genericDef == typeof(Task<>) || genericDef == typeof(ValueTask<>))
            {
                return returnType.GetGenericArguments()[0];
            }
        }

        return returnType;
    }
}