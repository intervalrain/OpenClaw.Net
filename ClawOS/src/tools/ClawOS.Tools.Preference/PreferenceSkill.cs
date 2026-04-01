using System.ComponentModel;
using System.Text;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ClawOS.Contracts.Skills;
using ClawOS.Contracts.Users.Commands;
using ClawOS.Contracts.Users.Queries;

namespace ClawOS.Tools.Preference;

public class PreferenceSkill(IServiceProvider serviceProvider) : AgentToolBase<PreferenceSkillArgs>
{
    public override string Name => "preference";
    public override string Description => """
        Manage user preferences. Use to store and retrieve user settings like:
        - ado.assignedTo, ado.organization, ado.project (Azure DevOps settings)
        - user.language, user.timezone (Personal settings)
        - Any custom key-value preferences
        Operations: get, set, list, delete
        """;

    public override async Task<ToolResult> ExecuteAsync(PreferenceSkillArgs args, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            return args.Operation.ToLowerInvariant() switch
            {
                "get" => await GetPreferenceAsync(mediator, args.Key, ct),
                "set" => await SetPreferenceAsync(mediator, args.Key, args.Value, ct),
                "list" => await ListPreferencesAsync(mediator, args.Prefix, ct),
                "delete" => await DeletePreferenceAsync(mediator, args.Key, ct),
                _ => ToolResult.Failure($"Unknown operation: {args.Operation}. Valid: get, set, list, delete")
            };
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Error: {ex.Message}");
        }
    }

    private static async Task<ToolResult> GetPreferenceAsync(
        ISender mediator,
        string? key,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return ToolResult.Failure("Key is required for get operation.");

        var query = new GetUserPreferenceQuery(key);
        var result = await mediator.Send(query, ct);

        return result.Match(
            preference => preference is null
                ? ToolResult.Success($"Preference '{key}' not found.")
                : ToolResult.Success($"{preference.Key} = {preference.Value ?? "(empty)"}"),
            errors => ToolResult.Failure(errors.First().Description));
    }

    private static async Task<ToolResult> SetPreferenceAsync(
        ISender mediator,
        string? key,
        string? value,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return ToolResult.Failure("Key is required for set operation.");

        var command = new SetUserPreferenceCommand(key, value);
        var result = await mediator.Send(command, ct);

        return result.Match(
            _ => ToolResult.Success($"Preference '{key}' set to '{value ?? "(empty)"}'."),
            errors => ToolResult.Failure(errors.First().Description));
    }

    private static async Task<ToolResult> ListPreferencesAsync(
        ISender mediator,
        string? prefix,
        CancellationToken ct)
    {
        var query = new ListUserPreferencesQuery(prefix);
        var result = await mediator.Send(query, ct);

        return result.Match(
            preferences =>
            {
                if (preferences.Count == 0)
                {
                    return ToolResult.Success(prefix is null
                        ? "No preferences found."
                        : $"No preferences found with prefix '{prefix}'.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"User Preferences ({preferences.Count}):");
                foreach (var p in preferences)
                {
                    sb.AppendLine($"  {p.Key} = {p.Value ?? "(empty)"}");
                }

                return ToolResult.Success(sb.ToString());
            },
            errors => ToolResult.Failure(errors.First().Description));
    }

    private static async Task<ToolResult> DeletePreferenceAsync(
        ISender mediator,
        string? key,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return ToolResult.Failure("Key is required for delete operation.");

        var command = new DeleteUserPreferenceCommand(key);
        var result = await mediator.Send(command, ct);

        return result.Match(
            _ => ToolResult.Success($"Preference '{key}' deleted."),
            errors => ToolResult.Failure(errors.First().Description));
    }
}

public record PreferenceSkillArgs(
    [property: Description("""
        The operation to perform:
        - get: Get a specific preference by key
        - set: Set a preference value (creates if not exists)
        - list: List all preferences (optional prefix filter)
        - delete: Delete a preference by key
        """)]
    string Operation,

    [property: Description("The preference key (e.g., 'ado.assignedTo', 'user.language')")]
    string? Key = null,

    [property: Description("The value to set (for set operation)")]
    string? Value = null,

    [property: Description("Key prefix for list operation (e.g., 'ado.' to list all ADO preferences)")]
    string? Prefix = null
);
