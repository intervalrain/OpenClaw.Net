using System.Reflection;
using Asp.Versioning;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

using Weda.Core.Infrastructure.Messaging;
using Weda.Core.Infrastructure.Observability;

namespace Weda.Core;

public class WedaCoreOptions
{
    public ApiVersion DefaultApiVersion { get; set; } = new(1, 0);

    public string ApiVersionGroupNameFormat { get; set; } = "'v'VVV";

    public List<Assembly> XmlCommentAssemblies { get; set; } = [];

    public Action<SwaggerGenOptions>? ConfigureSwagger { get; set; }

    public WedaMessagingOptions Messaging { get; } = new();

    public OpenApiInfo OpenApiInfo { get; set; } = new();

    public ObservabilityOptions Observability { get; } = new();
}

public class WedaCoreAppOptions
{
    public bool EnsureDatabaseCreated { get; set; } = false;

    public string SwaggerEndpointUrl { get; set; } = "/swagger/v1/swagger.json";

    public string SwaggerEndpointName { get; set; } = "API V1";

    public string RoutePrefix { get; set; } = string.Empty;

    public Action<SwaggerUIOptions>? ConfigureSwaggerUI { get; set; }
}
