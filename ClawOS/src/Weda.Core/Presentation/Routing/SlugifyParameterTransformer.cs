using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Routing;

namespace Weda.Core.Presentation.Routing;

public partial class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        if (value is null)
            return null;

        var str = value.ToString();
        if (string.IsNullOrEmpty(str))
            return null;

        // Convert PascalCase to kebab-case
        // e.g., "ModelProvider" -> "model-providers"
        // e.g., "ChatController" -> "chat"

        // Remove "Controller" suffix if present
        if (str.EndsWith("Controller", StringComparison.Ordinal))
            str = str[..^10];

        // Insert hyphen before each uppercase letter (except first)
        // e.g., "ModelProvider" -> "Model-Provider" -> "model-provider"
        return SlugifyRegex().Replace(str, "$1-$2").ToLowerInvariant();
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex SlugifyRegex();
}
