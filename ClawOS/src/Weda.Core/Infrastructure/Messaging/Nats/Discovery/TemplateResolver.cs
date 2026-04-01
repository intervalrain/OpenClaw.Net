using System.Reflection;
using System.Text.RegularExpressions;

using Asp.Versioning;

namespace Weda.Core.Infrastructure.Messaging.Nats.Discovery;

/// <summary>
/// Resolve naming template like [controller], [action], [method], {version}, {id}.
/// Supports named placeholders that become wildcards for NATS subscription
/// and can be parsed from incoming subjects.
/// </summary>
public static partial class TemplateResolver
{
    // Matches {name} or {name:constraint} placeholders
    [GeneratedRegex(@"\{(\w+)(?::\w+)?\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Resolves template with controller type, extracting version from ApiVersionAttribute.
    /// Named placeholders like {id} become * wildcards for NATS subscription.
    /// </summary>
    public static string Resolve(
        string template,
        Type controllerType,
        string? actionName = null)
    {
        var controllerName = controllerType.Name;
        var version = GetApiVersion(controllerType);

        return Resolve(template, controllerName, actionName, version);
    }

    /// <summary>
    /// Resolves template with explicit values.
    /// Named placeholders like {id} become * wildcards for NATS subscription.
    /// </summary>
    public static string Resolve(
        string template,
        string controllerName,
        string? actionName = null,
        string? version = null)
    {
        var result = template;

        var cleanControllerName = controllerName
            .Replace("EventController", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Controller", string.Empty, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("[controller]", cleanControllerName, StringComparison.OrdinalIgnoreCase);

        if (actionName is not null)
        {
            result = result.Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);
            result = result.Replace("[method]", actionName, StringComparison.OrdinalIgnoreCase);
        }

        if (version is not null)
        {
            result = result.Replace("{version:apiVersion}", version, StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{version}", version, StringComparison.OrdinalIgnoreCase);
        }

        // Convert remaining placeholders like {id} to * wildcard for NATS subscription
        result = PlaceholderRegex().Replace(result, "*");

        return result.ToLowerInvariant();
    }

    public static string Resolve(string controllerName)
    {
        return RemovePostfixes(controllerName, "EventController", "Controller");
    }

    /// <summary>
    /// Extracts placeholder names from a template pattern.
    /// E.g., "[controller].v{version:apiVersion}.{id}.get" returns ["id"] (excludes version).
    /// </summary>
    public static string[] GetPlaceholderNames(string template)
    {
        var matches = PlaceholderRegex().Matches(template);
        return matches
            .Select(m => m.Groups[1].Value)
            .Where(name => !name.Equals("version", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Parses an incoming subject against a resolved pattern to extract placeholder values.
    /// E.g., pattern "employee.v1.*.get", subject "employee.v1.123.get" returns {"id": "123"}.
    /// </summary>
    public static Dictionary<string, string> ParseSubject(
        string subjectPattern,
        Type controllerType,
        string actualSubject)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Get placeholder names from original pattern before resolution
        var placeholderNames = GetPlaceholderNames(subjectPattern);
        if (placeholderNames.Length == 0)
        {
            return result;
        }

        // Resolve the pattern (placeholders become *)
        var resolvedPattern = Resolve(subjectPattern, controllerType);

        // Split both pattern and subject by dots
        var patternParts = resolvedPattern.Split('.');
        var subjectParts = actualSubject.Split('.');

        if (patternParts.Length != subjectParts.Length)
        {
            return result;
        }

        // Find wildcard positions and extract values
        var placeholderIndex = 0;
        for (var i = 0; i < patternParts.Length && placeholderIndex < placeholderNames.Length; i++)
        {
            if (patternParts[i] == "*")
            {
                result[placeholderNames[placeholderIndex]] = subjectParts[i];
                placeholderIndex++;
            }
        }

        return result;
    }

    public static string? GetApiVersion(Type type)
    {
        var attr = type.GetCustomAttribute<ApiVersionAttribute>();
        return attr?.Versions.FirstOrDefault()?.ToString();
    }

    private static string RemovePostfixes(string pattern, params string[] postfixes)
    {
        foreach (var postfix in postfixes)
        {
            if (pattern.EndsWith(postfix))
            {
                pattern = pattern[..^postfix.Length];
            }
        }
        return pattern;
    }

    private static string RemovePrefixes(string pattern, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (pattern.StartsWith(prefix))
            {
                pattern = pattern[prefix.Length..];
            }
        }
        return pattern;
    }
}