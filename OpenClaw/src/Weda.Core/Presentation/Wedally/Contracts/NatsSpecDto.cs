namespace Weda.Core.Presentation.Wedally.Contracts;

/// <summary>
/// NATS API Specification - Similar to OpenAPI/Swagger spec.
/// </summary>
public record NatsSpecDto
{
    /// <summary>
    /// Spec version (e.g., "1.0.0").
    /// </summary>
    public string SpecVersion { get; init; } = "1.0.0";

    /// <summary>
    /// API information.
    /// </summary>
    public required NatsApiInfoDto Info { get; init; }

    /// <summary>
    /// Available servers/connections.
    /// </summary>
    public List<NatsServerDto> Servers { get; init; } = [];

    /// <summary>
    /// All endpoints grouped by tag (controller).
    /// </summary>
    public Dictionary<string, List<NatsEndpointSpecDto>> Paths { get; init; } = [];

    /// <summary>
    /// Type definitions (schemas).
    /// </summary>
    public Dictionary<string, TypeDefinitionDto> Definitions { get; init; } = [];

    /// <summary>
    /// Available API versions.
    /// </summary>
    public List<string> Versions { get; init; } = [];

    /// <summary>
    /// Tags (controllers) with descriptions.
    /// </summary>
    public List<NatsTagDto> Tags { get; init; } = [];
}

/// <summary>
/// API information.
/// </summary>
public record NatsApiInfoDto
{
    /// <summary>
    /// API title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// API description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// API version.
    /// </summary>
    public required string Version { get; init; }
}

/// <summary>
/// Server/connection information.
/// </summary>
public record NatsServerDto
{
    /// <summary>
    /// Connection name (e.g., "default", "bus").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Server URL.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Tag (controller group) information.
/// </summary>
public record NatsTagDto
{
    /// <summary>
    /// Tag name (controller name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Number of endpoints in this tag.
    /// </summary>
    public int EndpointCount { get; init; }
}

/// <summary>
/// NATS endpoint specification.
/// </summary>
public record NatsEndpointSpecDto
{
    /// <summary>
    /// Unique operation ID.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>
    /// Method name.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Summary/description.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Action type: Request, Publish, Consume, Fetch.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// HTTP method for Request action (GET, POST, PUT, PATCH, DELETE).
    /// Inferred from method name convention if not explicitly specified.
    /// </summary>
    public string? HttpMethod { get; init; }

    /// <summary>
    /// Subject pattern (e.g., "employee.v1.{id}.get").
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Resolved subject with wildcards.
    /// </summary>
    public required string ResolvedSubject { get; init; }

    /// <summary>
    /// API version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Connection name.
    /// </summary>
    public required string Connection { get; init; }

    /// <summary>
    /// Stream name (for JetStream).
    /// </summary>
    public string? Stream { get; init; }

    /// <summary>
    /// Consumer name (for JetStream).
    /// </summary>
    public string? Consumer { get; init; }

    /// <summary>
    /// Tag (controller name).
    /// </summary>
    public required string Tag { get; init; }

    /// <summary>
    /// Subject parameters (placeholders).
    /// </summary>
    public List<NatsParameterDto> Parameters { get; init; } = [];

    /// <summary>
    /// Request body schema reference.
    /// </summary>
    public NatsSchemaRefDto? RequestBody { get; init; }

    /// <summary>
    /// Response schema reference (for Request action).
    /// </summary>
    public NatsSchemaRefDto? Response { get; init; }

    /// <summary>
    /// Whether this endpoint can be tested (Request-Reply only).
    /// </summary>
    public bool CanTest { get; init; }
}

/// <summary>
/// Parameter information.
/// </summary>
public record NatsParameterDto
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parameter location: "subject" or "body".
    /// </summary>
    public required string In { get; init; }

    /// <summary>
    /// Parameter type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Schema reference.
/// </summary>
public record NatsSchemaRefDto
{
    /// <summary>
    /// Reference to definition (e.g., "#/definitions/GetEmployeeResponse").
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Schema name for display.
    /// </summary>
    public required string Schema { get; init; }
}

/// <summary>
/// Type definition (schema).
/// </summary>
public record TypeDefinitionDto
{
    /// <summary>
    /// Type name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full type name.
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// Type kind: "object", "enum", "primitive".
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Properties (for object types).
    /// </summary>
    public List<TypePropertyDto> Properties { get; init; } = [];

    /// <summary>
    /// Enum values (for enum types).
    /// </summary>
    public List<string>? EnumValues { get; init; }

    /// <summary>
    /// Example JSON.
    /// </summary>
    public string? Example { get; init; }
}

/// <summary>
/// Type property.
/// </summary>
public record TypePropertyDto
{
    /// <summary>
    /// Property name (camelCase).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Property type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the property is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Whether this is a nullable type.
    /// </summary>
    public bool Nullable { get; init; }

    /// <summary>
    /// Description from XML comments.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Reference to another definition.
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Example value for this property.
    /// </summary>
    public object? Example { get; init; }
}
