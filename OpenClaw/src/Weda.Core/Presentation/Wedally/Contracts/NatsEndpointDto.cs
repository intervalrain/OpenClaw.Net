namespace Weda.Core.Presentation.Wedally.Contracts;

/// <summary>
/// DTO for NATS endpoint metadata displayed in Wedally UI.
/// </summary>
public record NatsEndpointDto
{
    /// <summary>
    /// Unique identifier for the endpoint (e.g., "EmployeeEventController_GetEmployee").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Controller name without suffix (e.g., "Employee").
    /// </summary>
    public required string ControllerName { get; init; }

    /// <summary>
    /// Method name (e.g., "GetEmployee").
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// Original subject pattern with placeholders (e.g., "[controller].v{version:apiVersion}.{id}.get").
    /// </summary>
    public required string SubjectPattern { get; init; }

    /// <summary>
    /// Resolved subject pattern with wildcards (e.g., "employee.v1.*.get").
    /// </summary>
    public required string ResolvedSubject { get; init; }

    /// <summary>
    /// Endpoint mode: RequestReply, JetStreamConsume, JetStreamFetch, or CorePubSub.
    /// </summary>
    public required string Mode { get; init; }

    /// <summary>
    /// NATS connection name (e.g., "default", "bus").
    /// </summary>
    public required string ConnectionName { get; init; }

    /// <summary>
    /// JetStream stream name if applicable.
    /// </summary>
    public string? StreamName { get; init; }

    /// <summary>
    /// JetStream consumer name if applicable.
    /// </summary>
    public string? ConsumerName { get; init; }

    /// <summary>
    /// Request type schema information.
    /// </summary>
    public TypeSchemaDto? RequestType { get; init; }

    /// <summary>
    /// Response type schema information.
    /// </summary>
    public TypeSchemaDto? ResponseType { get; init; }

    /// <summary>
    /// Placeholder names extracted from subject pattern (e.g., ["id"]).
    /// </summary>
    public string[] Placeholders { get; init; } = [];
}

/// <summary>
/// Response containing all discovered NATS endpoints.
/// </summary>
public record NatsEndpointsResponse(
    List<NatsEndpointDto> Endpoints,
    Dictionary<string, List<NatsEndpointDto>> GroupedByController);
