using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Weda.Core.Presentation.Swagger;

public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodHasAllowAnonymous = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AllowAnonymousAttribute>()
            .Any();

        var methodHasAuthorize = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any();

        if (methodHasAllowAnonymous)
        {
            return;
        }

        if (methodHasAuthorize)
        {
            AddSecurityRequirement(operation, context);
            return;
        }

        var controllerType = context.MethodInfo.DeclaringType;
        while (controllerType != null)
        {
            var controllerHasAllowAnonymous = controllerType
                .GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any();

            var controllerHasAuthorize = controllerType
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any();

            if (controllerHasAllowAnonymous)
            {
                return;
            }

            if (controllerHasAuthorize)
            {
                AddSecurityRequirement(operation, context);
                return;
            }

            controllerType = controllerType.BaseType;
        }
    }

    private static void AddSecurityRequirement(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Security ??= [];

        var requirement = new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer", context.Document), [] },
        };

        operation.Security.Add(requirement);
    }
}
