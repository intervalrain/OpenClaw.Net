using System.ComponentModel;
using System.Text;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Contracts.Users.Queries;

namespace OpenClaw.Skills.Preference;

public class PreferenceSkill(IServiceProvider serviceProvider) : AgentSkillBase<PreferenceSkillArgs>
{
    public override string Name => "preference";
    public override string Description => """
        Manage user preferences. Use to store and retrieve user settings like:
        - ado.assignedTo, ado.organization, ado.project (Azure DevOps settings)
        - user.language, user.timezone (Personal settings)
        - Any custom key-value preferences
        Operations: get, set, list, delete
        """;

    public override async Task<SkillResult> ExecuteAsync(PreferenceSkillArgs args, CancellationToken ct)
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
                _ => SkillResult.Failure($"Unknown operation: {args.Operation}. Valid: get, set, list, delete")
            };
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Error: {ex.Message}");
        }
    }

    private static async Task<SkillResult> GetPreferenceAsync(
        ISender mediator,
        string? key,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return SkillResult.Failure("Key is required for get operation.");

        var query = new GetUserPreferenceQuery(key);
        var result = await mediator.Send(query, ct);

        return result.Match(
            preference => preference is null
                ? SkillResult.Success($"Preference '{key}' not found.")
                : SkillResult.Success($"{preference.Key} = {preference.Value ?? "(empty)"}"),
            errors => SkillResult.Failure(errors.First().Description));
    }

    private static async Task<SkillResult> SetPreferenceAsync(
        ISender mediator,
        string? key,
        string? value,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return SkillResult.Failure("Key is required for set operation.");

        var command = new SetUserPreferenceCommand(key, value);
        var result = await mediator.Send(command, ct);

        return result.Match(
            _ => SkillResult.Success($"Preference '{key}' set to '{value ?? "(empty)"}'."),
            errors => SkillResult.Failure(errors.First().Description));
    }

    private static async Task<SkillResult> ListPreferencesAsync(
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
                    return SkillResult.Success(prefix is null
                        ? "No preferences found."
                        : $"No preferences found with prefix '{prefix}'.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"User Preferences ({preferences.Count}):");
                foreach (var p in preferences)
                {
                    sb.AppendLine($"  {p.Key} = {p.Value ?? "(empty)"}");
                }

                return SkillResult.Success(sb.ToString());
            },
            errors => SkillResult.Failure(errors.First().Description));
    }

    private static async Task<SkillResult> DeletePreferenceAsync(
        ISender mediator,
        string? key,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return SkillResult.Failure("Key is required for delete operation.");

        var command = new DeleteUserPreferenceCommand(key);
        var result = await mediator.Send(command, ct);

        return result.Match(
            _ => SkillResult.Success($"Preference '{key}' deleted."),
            errors => SkillResult.Failure(errors.First().Description));
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
