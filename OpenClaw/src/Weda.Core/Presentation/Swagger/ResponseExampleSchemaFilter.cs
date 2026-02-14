using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;


namespace Weda.Core.Presentation.Swagger;

public class ResponseExampleSchemaFilter(IServiceProvider serviceProvider) : ISchemaFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema)
        {
            return;
        }

        var exampleProviderType = typeof(IExamplesProvider<>).MakeGenericType(context.Type);
        var exampleProvider = serviceProvider.GetService(exampleProviderType);

        if (exampleProvider is null)
        {
            return;
        }

        var getExamplesMethod = exampleProviderType.GetMethod("GetExamples");
        var example = getExamplesMethod?.Invoke(exampleProvider, null);

        if (example is not null)
        {
            var json = JsonSerializer.Serialize(example, JsonOptions);
            concreteSchema.Example = JsonNode.Parse(json);
        }
    }
}
