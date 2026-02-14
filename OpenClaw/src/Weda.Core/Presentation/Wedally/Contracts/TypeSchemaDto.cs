namespace Weda.Core.Presentation.Wedally.Contracts;

/// <summary>
/// DTO for type schema information.
/// </summary>
public record TypeSchemaDto
{
    /// <summary>
    /// Type name (e.g., "GetEmployeeResponse", "int").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full type name including namespace.
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// Whether this type is extracted from subject placeholder (e.g., {id}).
    /// </summary>
    public bool IsFromSubject { get; init; }

    /// <summary>
    /// Placeholder name if IsFromSubject is true.
    /// </summary>
    public string? PlaceholderName { get; init; }

    /// <summary>
    /// Properties of the type if it's a complex type.
    /// </summary>
    public List<PropertySchemaDto> Properties { get; init; } = [];
}

/// <summary>
/// DTO for property schema information.
/// </summary>
public record PropertySchemaDto
{
    /// <summary>
    /// Property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Property type name.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the property is required (non-nullable).
    /// </summary>
    public bool IsRequired { get; init; }
}
