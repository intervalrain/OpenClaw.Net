using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Swashbuckle.AspNetCore.Filters;
using Weda.Core.Application.Interfaces.Messaging;
using Weda.Core.Infrastructure.Messaging.Nats.Discovery;
using Weda.Core.Infrastructure.Messaging.Nats.Enums;
using Weda.Core.Infrastructure.Middleware;
using Weda.Core.Presentation.Wedally.Contracts;

namespace Weda.Core.Presentation.Wedally;

/// <summary>
/// Wedally - NATS EventController testing UI API.
/// Provides endpoints for discovering and testing NATS message handlers.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wedally")]
[ApiExplorerSettings(IgnoreApi = true)]
[SkipTransaction]
public class WedallyController(
    EventControllerDiscovery discovery,
    IJetStreamClientFactory clientFactory,
    IServiceProvider serviceProvider,
    ILogger<WedallyController> logger) : ApiController
{
    private static readonly HashSet<Type> ProcessedTypes = [];

    /// <summary>
    /// Returns the NATS API specification (similar to OpenAPI/Swagger spec).
    /// </summary>
    /// <returns>Complete NATS API specification.</returns>
    [HttpGet("spec")]
    [ProducesResponseType(typeof(NatsSpecDto), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public ActionResult<NatsSpecDto> GetSpec()
    {
        ProcessedTypes.Clear();
        var definitions = new Dictionary<string, TypeDefinitionDto>();

        var endpoints = discovery.Endpoints
            .Select(e => MapToSpecDto(e, definitions))
            .ToList();

        // Group by tag (controller)
        var paths = endpoints
            .GroupBy(e => e.Tag)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Extract versions
        var versions = endpoints
            .Select(e => e.Version)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        // Build tags
        var tags = paths
            .Select(kvp => new NatsTagDto
            {
                Name = kvp.Key,
                Description = $"{kvp.Key} EventController endpoints",
                EndpointCount = kvp.Value.Count
            })
            .OrderBy(t => t.Name)
            .ToList();

        // Get unique connections
        var connections = endpoints
            .Select(e => e.Connection)
            .Distinct()
            .Select(c => new NatsServerDto
            {
                Name = c,
                Description = c == "default" ? "Default NATS connection" : $"NATS connection: {c}"
            })
            .ToList();

        var spec = new NatsSpecDto
        {
            Info = new NatsApiInfoDto
            {
                Title = "Weda NATS API",
                Description = "NATS EventController endpoints specification",
                Version = "1.0.0"
            },
            Servers = connections,
            Paths = paths,
            Definitions = definitions,
            Versions = versions,
            Tags = tags
        };

        return Ok(spec);
    }

    /// <summary>
    /// Lists all discovered NATS endpoints with metadata.
    /// </summary>
    /// <returns>All NATS endpoints grouped by controller.</returns>
    [HttpGet("endpoints")]
    [ProducesResponseType(typeof(NatsEndpointsResponse), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public ActionResult<NatsEndpointsResponse> GetEndpoints()
    {
        var endpoints = discovery.Endpoints
            .Select(MapToDto)
            .ToList();

        var grouped = endpoints
            .GroupBy(e => e.ControllerName)
            .ToDictionary(g => g.Key, g => g.ToList());

        return Ok(new NatsEndpointsResponse(endpoints, grouped));
    }

    /// <summary>
    /// Publishes a message to a NATS subject and returns the response.
    /// Only fully supports Request-Reply endpoints.
    /// </summary>
    /// <param name="request">The publish request containing endpoint ID, subject, and payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the NATS handler.</returns>
    [HttpPost("publish")]
    [ProducesResponseType(typeof(NatsPublishResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<ActionResult<NatsPublishResponse>> Publish(
        [FromBody] NatsPublishRequest request,
        CancellationToken cancellationToken)
    {
        var endpoint = discovery.Endpoints
            .FirstOrDefault(e => GetEndpointId(e) == request.EndpointId);

        if (endpoint is null)
        {
            return NotFound($"Endpoint '{request.EndpointId}' not found");
        }

        if (endpoint.Mode != EndpointMode.RequestReply)
        {
            return BadRequest($"Only Request-Reply endpoints support testing with response. " +
                $"This endpoint is {endpoint.Mode}. Messages will be sent but no response is expected.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = clientFactory.Create(endpoint.ConnectionName);

            // Convert JsonElement to byte[] for direct transmission
            byte[]? payloadBytes = request.Payload.HasValue
                ? JsonSerializer.SerializeToUtf8Bytes(request.Payload.Value, WedaJsonDefaults.Options)
                : null;

            logger.LogInformation("Sending request to {Subject}, payload bytes: {Length}",
                request.Subject,
                payloadBytes?.Length ?? -1);

            // Use IJetStreamClient which auto-injects trace headers
            var response = await client.RequestAsync<byte[]?, JsonElement?>(
                request.Subject,
                payloadBytes,
                TimeSpan.FromMilliseconds(request.TimeoutMs),
                cancellationToken);

            stopwatch.Stop();

            return Ok(new NatsPublishResponse
            {
                Success = true,
                Subject = request.Subject,
                ResponseData = response.Data,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (NatsNoRespondersException)
        {
            stopwatch.Stop();
            logger.LogWarning("No responders for subject {Subject}", request.Subject);

            return Ok(new NatsPublishResponse
            {
                Success = false,
                Subject = request.Subject,
                ErrorCode = 503,
                ErrorMessage = "No responders available for this subject. Ensure the NATS handler is running.",
                ElapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning("Request to {Subject} timed out after {Timeout}ms",
                request.Subject, request.TimeoutMs);

            return Ok(new NatsPublishResponse
            {
                Success = false,
                Subject = request.Subject,
                ErrorCode = 408,
                ErrorMessage = $"Request timed out after {request.TimeoutMs}ms",
                ElapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Error publishing to {Subject}", request.Subject);

            return Ok(new NatsPublishResponse
            {
                Success = false,
                Subject = request.Subject,
                ErrorCode = 500,
                ErrorMessage = ex.Message,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
    }

    /// <summary>
    /// Fire-and-forget publish to a NATS subject.
    /// For Publish, Consume, and Fetch endpoints that don't expect a response.
    /// Always returns success if the message was sent.
    /// </summary>
    /// <param name="request">The publish request containing endpoint ID, subject, and payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success response indicating message was sent.</returns>
    [HttpPost("fire")]
    [ProducesResponseType(typeof(NatsPublishResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<NatsPublishResponse>> Fire(
        [FromBody] NatsPublishRequest request,
        CancellationToken cancellationToken)
    {
        var endpoint = discovery.Endpoints
            .FirstOrDefault(e => GetEndpointId(e) == request.EndpointId);

        if (endpoint is null)
        {
            return NotFound($"Endpoint '{request.EndpointId}' not found");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = clientFactory.Create(endpoint.ConnectionName);

            // For JetStream endpoints, use JetStream publish to ensure message persistence
            var isJetStream = endpoint.Mode == EndpointMode.JetStreamConsume ||
                              endpoint.Mode == EndpointMode.JetStreamFetch;

            // Convert JsonElement to byte[] for direct transmission
            byte[]? payloadBytes = request.Payload.HasValue
                ? JsonSerializer.SerializeToUtf8Bytes(request.Payload.Value, WedaJsonDefaults.Options)
                : null;

            logger.LogInformation(
                "Fire-and-forget publish to {Subject}, payload bytes: {Length}, JetStream: {IsJetStream}",
                request.Subject,
                payloadBytes?.Length ?? -1,
                isJetStream);

            // Use IJetStreamClient which auto-injects trace headers
            if (isJetStream)
            {
                await client.JsPublishAsync(request.Subject, payloadBytes, cancellationToken);
            }
            else
            {
                await client.PublishAsync(request.Subject, payloadBytes, cancellationToken);
            }

            stopwatch.Stop();

            return Ok(new NatsPublishResponse
            {
                Success = true,
                Subject = request.Subject,
                ResponseData = null,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Error publishing to {Subject}", request.Subject);

            return Ok(new NatsPublishResponse
            {
                Success = false,
                Subject = request.Subject,
                ErrorCode = 500,
                ErrorMessage = ex.Message,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
    }

    private static string GetEndpointId(EndpointDescriptor e) =>
        $"{e.ControllerType.Name}_{e.Method.Name}";

    private NatsEndpointSpecDto MapToSpecDto(EndpointDescriptor e, Dictionary<string, TypeDefinitionDto> definitions)
    {
        var placeholders = TemplateResolver.GetPlaceholderNames(e.SubjectPattern);
        var version = TemplateResolver.GetApiVersion(e.ControllerType) ?? "1";
        var controllerName = TemplateResolver.Resolve(e.ControllerType.Name);

        // Build parameters from placeholders
        var parameters = placeholders.Select(p => new NatsParameterDto
        {
            Name = p,
            In = "subject",
            Type = "string",
            Required = true,
            Description = $"Value for {{{p}}} in subject"
        }).ToList();

        // Map action type
        var action = e.Mode switch
        {
            EndpointMode.RequestReply => "Request",
            EndpointMode.CorePubSub => "Publish",
            EndpointMode.JetStreamConsume => "Consume",
            EndpointMode.JetStreamFetch => "Fetch",
            _ => "Unknown"
        };

        // Infer HTTP method for Request-Reply endpoints
        string? httpMethod = null;
        if (e.Mode == EndpointMode.RequestReply)
        {
            httpMethod = InferHttpMethod(e.Method);
        }

        // Build request body schema
        NatsSchemaRefDto? requestBody = null;
        if (e.RequestType is not null && !IsPrimitiveType(e.RequestType))
        {
            var typeName = GetTypeName(e.RequestType);
            AddTypeDefinition(e.RequestType, definitions);
            requestBody = new NatsSchemaRefDto
            {
                Ref = $"#/definitions/{typeName}",
                Schema = typeName
            };
        }

        // Build response schema
        NatsSchemaRefDto? response = null;
        if (e.ResponseType is not null)
        {
            var typeName = GetTypeName(e.ResponseType);
            if (!IsPrimitiveType(e.ResponseType))
            {
                AddTypeDefinition(e.ResponseType, definitions);
            }
            response = new NatsSchemaRefDto
            {
                Ref = IsPrimitiveType(e.ResponseType) ? null : $"#/definitions/{typeName}",
                Schema = typeName
            };
        }

        return new NatsEndpointSpecDto
        {
            OperationId = GetEndpointId(e),
            Method = e.Method.Name,
            Summary = GetMethodSummary(e.Method),
            Action = action,
            HttpMethod = httpMethod,
            Subject = e.SubjectPattern,
            ResolvedSubject = TemplateResolver.Resolve(e.SubjectPattern, e.ControllerType),
            Version = $"v{version}",
            Connection = e.ConnectionName,
            Stream = e.StreamName,
            Consumer = e.ConsumerName,
            Tag = controllerName,
            Parameters = parameters,
            RequestBody = requestBody,
            Response = response,
            CanTest = e.Mode == EndpointMode.RequestReply
        };
    }

    private void AddTypeDefinition(Type type, Dictionary<string, TypeDefinitionDto> definitions)
    {
        if (type is null || IsPrimitiveType(type)) return;

        var typeName = GetTypeName(type);
        if (definitions.ContainsKey(typeName)) return;
        if (ProcessedTypes.Contains(type)) return;

        ProcessedTypes.Add(type);

        // Handle collections
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) ||
                genericDef == typeof(IEnumerable<>) || genericDef == typeof(ICollection<>))
            {
                var elementType = type.GetGenericArguments()[0];
                AddTypeDefinition(elementType, definitions);
                return;
            }
        }

        // Handle enums
        if (type.IsEnum)
        {
            definitions[typeName] = new TypeDefinitionDto
            {
                Name = typeName,
                FullName = type.FullName,
                Kind = "enum",
                EnumValues = Enum.GetNames(type).ToList()
            };
            return;
        }

        // Try to get example from IExamplesProvider
        var exampleInstance = GetExampleFromProvider(type);

        // Handle objects
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p =>
            {
                var propTypeName = GetTypeName(p.PropertyType);
                var isComplex = !IsPrimitiveType(p.PropertyType);

                // Recursively add nested types
                if (isComplex)
                {
                    AddTypeDefinition(p.PropertyType, definitions);
                }

                // Get example value for this property
                object? propExample = null;
                if (exampleInstance is not null)
                {
                    propExample = p.GetValue(exampleInstance);
                }

                var isNullable = IsPropertyNullable(p);
                var description = GetPropertyDescription(type, p.Name);

                return new TypePropertyDto
                {
                    Name = ToCamelCase(p.Name),
                    Type = propTypeName,
                    Required = !isNullable,
                    Nullable = isNullable,
                    Description = description,
                    Ref = isComplex ? $"#/definitions/{propTypeName}" : null,
                    Example = propExample
                };
            })
            .ToList();

        definitions[typeName] = new TypeDefinitionDto
        {
            Name = typeName,
            FullName = type.FullName,
            Kind = "object",
            Properties = properties,
            Example = GenerateExampleJson(exampleInstance, type, properties)
        };
    }

    private static string? GetMethodSummary(MethodInfo method)
    {
        // Try to get summary from XML documentation (if available)
        // For now, generate from method name
        var name = method.Name;

        // Convert PascalCase to sentence
        var result = System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
        return result;
    }

    /// <summary>
    /// Gets description for a property from XML documentation.
    /// For records, reads from the constructor parameter's XML doc.
    /// </summary>
    private static string? GetPropertyDescription(Type type, string propertyName)
    {
        try
        {
            // For records, the XML documentation is on the constructor parameter
            // Try to load XML documentation file
            var xmlPath = Path.ChangeExtension(type.Assembly.Location, ".xml");
            if (!System.IO.File.Exists(xmlPath)) return null;

            var xml = System.Xml.Linq.XDocument.Load(xmlPath);
            var members = xml.Root?.Element("members");
            if (members is null) return null;

            // For records, look for the type's XML doc with <param> tags
            var typeMemberName = $"T:{type.FullName}";
            var typeMember = members.Elements("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == typeMemberName);

            if (typeMember is not null)
            {
                // Look for <param name="PropertyName"> tag
                var paramElement = typeMember.Elements("param")
                    .FirstOrDefault(p => string.Equals(p.Attribute("name")?.Value, propertyName, StringComparison.OrdinalIgnoreCase));

                if (paramElement is not null)
                {
                    return paramElement.Value.Trim();
                }
            }

            // Fallback: try property documentation (P:Namespace.Type.Property)
            var propertyMemberName = $"P:{type.FullName}.{propertyName}";
            var propertyMember = members.Elements("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == propertyMemberName);

            if (propertyMember is not null)
            {
                var summary = propertyMember.Element("summary");
                if (summary is not null)
                {
                    return summary.Value.Trim();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets an example instance from IExamplesProvider if available.
    /// </summary>
    private object? GetExampleFromProvider(Type type)
    {
        try
        {
            // Find IExamplesProvider<T> for this type
            var providerType = typeof(IExamplesProvider<>).MakeGenericType(type);
            var provider = serviceProvider.GetService(providerType);

            if (provider is null)
            {
                // Try to find the example provider class in the same assembly
                var exampleProviderType = type.Assembly.GetTypes()
                    .FirstOrDefault(t => t.GetInterfaces()
                        .Any(i => i.IsGenericType &&
                                  i.GetGenericTypeDefinition() == typeof(IExamplesProvider<>) &&
                                  i.GetGenericArguments()[0] == type));

                if (exampleProviderType is not null)
                {
                    provider = Activator.CreateInstance(exampleProviderType);
                }
            }

            if (provider is null) return null;

            // Call GetExamples() method
            var getExamplesMethod = providerType.GetMethod("GetExamples");
            return getExamplesMethod?.Invoke(provider, null);
        }
        catch
        {
            return null;
        }
    }

    private static string? GenerateExampleJson(object? exampleInstance, Type type, List<TypePropertyDto> properties)
    {
        try
        {
            if (exampleInstance is not null)
            {
                return JsonSerializer.Serialize(exampleInstance, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            // Fallback: generate from property types
            var example = new Dictionary<string, object?>();
            foreach (var prop in properties)
            {
                example[prop.Name] = prop.Example ?? GetDefaultExampleValue(prop.Type);
            }
            return JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return null;
        }
    }

    private static object? GetDefaultExampleValue(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "string" => "string",
            "int" or "int32" => 0,
            "long" or "int64" => 0L,
            "bool" or "boolean" => false,
            "double" => 0.0,
            "float" => 0.0f,
            "decimal" => 0m,
            "datetime" => DateTime.UtcNow.ToString("O"),
            "guid" => Guid.Empty.ToString(),
            _ when type.EndsWith("[]") => Array.Empty<object>(),
            _ when type.EndsWith("?") => null,
            _ => new { }
        };
    }

    private static NatsEndpointDto MapToDto(EndpointDescriptor e)
    {
        var placeholders = TemplateResolver.GetPlaceholderNames(e.SubjectPattern);

        return new NatsEndpointDto
        {
            Id = GetEndpointId(e),
            ControllerName = TemplateResolver.Resolve(e.ControllerType.Name),
            MethodName = e.Method.Name,
            SubjectPattern = e.SubjectPattern,
            ResolvedSubject = TemplateResolver.Resolve(e.SubjectPattern, e.ControllerType),
            Mode = e.Mode.ToString(),
            ConnectionName = e.ConnectionName,
            StreamName = e.StreamName,
            ConsumerName = e.ConsumerName,
            RequestType = MapTypeSchema(e.RequestType, placeholders),
            ResponseType = MapTypeSchema(e.ResponseType, []),
            Placeholders = placeholders
        };
    }

    private static TypeSchemaDto? MapTypeSchema(Type? type, string[] placeholders)
    {
        if (type is null) return null;

        var isPrimitive = IsPrimitiveType(type);

        return new TypeSchemaDto
        {
            Name = GetTypeName(type),
            FullName = type.FullName,
            IsFromSubject = isPrimitive && placeholders.Length > 0,
            PlaceholderName = isPrimitive && placeholders.Length > 0
                ? placeholders.FirstOrDefault()
                : null,
            Properties = isPrimitive ? [] : GetTypeProperties(type)
        };
    }

    private static List<PropertySchemaDto> GetTypeProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new PropertySchemaDto
            {
                Name = ToCamelCase(p.Name),
                Type = GetTypeName(p.PropertyType),
                IsRequired = !IsNullable(p.PropertyType)
            })
            .ToList();
    }

    private static string GetTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(Nullable<>))
            {
                return GetTypeName(type.GetGenericArguments()[0]) + "?";
            }

            if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) ||
                genericDef == typeof(IEnumerable<>) || genericDef == typeof(ICollection<>))
            {
                return GetTypeName(type.GetGenericArguments()[0]) + "[]";
            }

            var baseName = type.Name[..type.Name.IndexOf('`')];
            var args = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
            return $"{baseName}<{args}>";
        }

        return type.Name switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "String" => "string",
            "Boolean" => "bool",
            "Double" => "double",
            "Single" => "float",
            "Decimal" => "decimal",
            "DateTime" => "DateTime",
            "Guid" => "Guid",
            _ => type.Name
        };
    }

    private static bool IsPrimitiveType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType.IsPrimitive ||
               underlyingType == typeof(string) ||
               underlyingType == typeof(decimal) ||
               underlyingType == typeof(DateTime) ||
               underlyingType == typeof(Guid);
    }

    private static bool IsNullable(Type type)
    {
        // For Nullable<T> value types
        if (Nullable.GetUnderlyingType(type) is not null) return true;

        // Value types that aren't Nullable<T> are not nullable
        if (type.IsValueType) return false;

        // Reference types - check if marked as nullable (can't determine without context)
        // For DTOs, we assume reference types are required unless marked with ?
        return false;
    }

    /// <summary>
    /// Checks if a property is nullable using NullabilityInfoContext (C# 8+ NRT support).
    /// </summary>
    private static bool IsPropertyNullable(PropertyInfo property)
    {
        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);
        return nullabilityInfo.WriteState == NullabilityState.Nullable ||
               nullabilityInfo.ReadState == NullabilityState.Nullable;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Infers HTTP method from method name using naming conventions.
    /// </summary>
    private static string InferHttpMethod(MethodInfo method)
    {
        // Check for explicit HTTP method attributes first
        if (method.GetCustomAttribute<HttpGetAttribute>() is not null) return "GET";
        if (method.GetCustomAttribute<HttpPostAttribute>() is not null) return "POST";
        if (method.GetCustomAttribute<HttpPutAttribute>() is not null) return "PUT";
        if (method.GetCustomAttribute<HttpPatchAttribute>() is not null) return "PATCH";
        if (method.GetCustomAttribute<HttpDeleteAttribute>() is not null) return "DELETE";

        // Infer from method name convention
        var name = method.Name;

        // GET: Get, Fetch, List, Find, Query, Search, Load, Read
        if (name.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Fetch", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("List", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Find", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Query", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Search", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Load", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Read", StringComparison.OrdinalIgnoreCase))
        {
            return "GET";
        }

        // POST: Create, Add, Insert, Submit, Send, Post
        if (name.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Add", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Insert", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Submit", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Send", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Post", StringComparison.OrdinalIgnoreCase))
        {
            return "POST";
        }

        // PUT: Update, Set, Replace, Revise, Modify
        if (name.StartsWith("Update", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Set", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Replace", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Revise", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Modify", StringComparison.OrdinalIgnoreCase))
        {
            return "PUT";
        }

        // PATCH: Patch, PartialUpdate
        if (name.StartsWith("Patch", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("PartialUpdate", StringComparison.OrdinalIgnoreCase))
        {
            return "PATCH";
        }

        // DELETE: Delete, Remove, Erase, Destroy, Cancel
        if (name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Remove", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Erase", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Destroy", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Cancel", StringComparison.OrdinalIgnoreCase))
        {
            return "DELETE";
        }

        // Default to POST for unknown patterns (most NATS operations are command-like)
        return "POST";
    }
}
