using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.CronJobs;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.CronJobs.Tools;

/// <summary>
/// Agent tool for managing cron jobs via chat.
/// Supports: create, list, update, delete, execute.
/// </summary>
public class ManageCronJobTool(IServiceScopeFactory scopeFactory) : AgentToolBase<ManageCronJobArgs>
{
    public override string Name => "manage_cronjob";
    public override string Description =>
        "Manage scheduled cron jobs. Actions: create (new job), list (show all), update (modify), delete (remove), execute (run now). " +
        "For create/update, provide schedule with frequency (Daily/Weekly/Monthly), timeOfDay (HH:mm), timezone, and optionally daysOfWeek.";

    public override async Task<ToolResult> ExecuteAsync(ManageCronJobArgs args, ToolContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Action))
            return ToolResult.Failure("Action is required (create, list, update, delete, execute).");

        var userId = context.UserId;
        if (userId is null || userId == Guid.Empty)
            return ToolResult.Failure("User context required.");

        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<ICronJobRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return args.Action.ToLowerInvariant() switch
        {
            "create" => await CreateAsync(args, userId.Value, jobRepo, uow, ct),
            "list" => await ListAsync(userId.Value, jobRepo, ct),
            "update" => await UpdateAsync(args, userId.Value, jobRepo, uow, ct),
            "delete" => await DeleteAsync(args, userId.Value, jobRepo, uow, ct),
            "execute" => await ExecuteNowAsync(args, userId.Value, scope, ct),
            _ => ToolResult.Failure($"Unknown action: {args.Action}. Use: create, list, update, delete, execute.")
        };
    }

    private static async Task<ToolResult> CreateAsync(
        ManageCronJobArgs args, Guid userId, ICronJobRepository repo, IUnitOfWork uow, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Name))
            return ToolResult.Failure("Name is required for create.");
        if (string.IsNullOrWhiteSpace(args.Content))
            return ToolResult.Failure("Content (prompt) is required for create.");

        var scheduleJson = args.Schedule is not null
            ? JsonSerializer.Serialize(args.Schedule, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : "{}";

        var contextJson = args.Skills is { Count: > 0 }
            ? JsonSerializer.Serialize(args.Skills)
            : null;

        var job = CronJob.Create(
            userId,
            args.Name,
            scheduleJson,
            args.Content,
            wakeMode: args.WakeMode ?? WakeMode.Both,
            contextJson: contextJson);

        await repo.AddAsync(job, ct);
        await uow.SaveChangesAsync(ct);

        return ToolResult.Success($"Created cron job '{args.Name}' (ID: {job.Id}). Schedule: {scheduleJson}");
    }

    private static async Task<ToolResult> ListAsync(Guid userId, ICronJobRepository repo, CancellationToken ct)
    {
        var jobs = await repo.GetAllByUserAsync(userId, ct);

        if (jobs.Count == 0)
            return ToolResult.Success("No cron jobs found.");

        var lines = jobs.Select(j =>
            $"- [{(j.IsActive ? "Active" : "Inactive")}] {j.Name} (ID: {j.Id})\n  Prompt: {Truncate(j.Content, 80)}\n  Schedule: {j.ScheduleJson}");

        return ToolResult.Success($"Found {jobs.Count} cron job(s):\n\n{string.Join("\n\n", lines)}");
    }

    private static async Task<ToolResult> UpdateAsync(
        ManageCronJobArgs args, Guid userId, ICronJobRepository repo, IUnitOfWork uow, CancellationToken ct)
    {
        if (args.JobId is null)
            return ToolResult.Failure("JobId is required for update.");

        var job = await repo.GetByIdAsync(args.JobId.Value, ct);
        if (job is null || job.CreatedByUserId != userId)
            return ToolResult.Failure("Job not found.");

        var scheduleJson = args.Schedule is not null
            ? JsonSerializer.Serialize(args.Schedule, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : null;

        job.Update(
            name: args.Name,
            scheduleJson: scheduleJson,
            content: args.Content,
            wakeMode: args.WakeMode,
            isActive: args.IsActive);

        await uow.SaveChangesAsync(ct);
        return ToolResult.Success($"Updated cron job '{job.Name}'.");
    }

    private static async Task<ToolResult> DeleteAsync(
        ManageCronJobArgs args, Guid userId, ICronJobRepository repo, IUnitOfWork uow, CancellationToken ct)
    {
        if (args.JobId is null)
            return ToolResult.Failure("JobId is required for delete.");

        var job = await repo.GetByIdAsync(args.JobId.Value, ct);
        if (job is null || job.CreatedByUserId != userId)
            return ToolResult.Failure("Job not found.");

        await repo.DeleteAsync(job, ct);
        await uow.SaveChangesAsync(ct);
        return ToolResult.Success($"Deleted cron job '{job.Name}'.");
    }

    private static async Task<ToolResult> ExecuteNowAsync(
        ManageCronJobArgs args, Guid userId, IServiceScope scope, CancellationToken ct)
    {
        if (args.JobId is null)
            return ToolResult.Failure("JobId is required for execute.");

        var jobRepo = scope.ServiceProvider.GetRequiredService<ICronJobRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<ICronJobExecutor>();

        var job = await jobRepo.GetByIdAsync(args.JobId.Value, ct);
        if (job is null || job.CreatedByUserId != userId)
            return ToolResult.Failure("Job not found.");

        var executionId = await executor.ExecuteAsync(job, userId, ExecutionTrigger.Manual, ct);
        return ToolResult.Success($"Triggered execution of '{job.Name}' (Execution ID: {executionId}).");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}

public record ManageCronJobArgs(
    [property: Description("Action to perform: create, list, update, delete, execute")]
    string? Action,

    [property: Description("Name of the cron job (for create/update)")]
    string? Name = null,

    [property: Description("The prompt text to execute (for create/update)")]
    string? Content = null,

    [property: Description("Schedule configuration (for create/update)")]
    ScheduleArgs? Schedule = null,

    [property: Description("List of @skill names to provide context (for create/update)")]
    List<string>? Skills = null,

    [property: Description("Wake mode: Scheduled, Manual, or Both (for create/update)")]
    WakeMode? WakeMode = null,

    [property: Description("Whether the job is active (for update)")]
    bool? IsActive = null,

    [property: Description("Job ID (for update/delete/execute)")]
    Guid? JobId = null
);

public record ScheduleArgs(
    [property: Description("Frequency: Daily, Weekly, or Monthly")]
    string? Frequency = null,

    [property: Description("Time of day in HH:mm format (e.g. 09:00)")]
    string? TimeOfDay = null,

    [property: Description("Timezone (e.g. Asia/Taipei, UTC)")]
    string? Timezone = null,

    [property: Description("Days of week for Weekly (e.g. Monday, Wednesday, Friday)")]
    List<string>? DaysOfWeek = null,

    [property: Description("Day of month for Monthly (1-31)")]
    int? DayOfMonth = null,

    [property: Description("Whether the schedule is enabled")]
    bool IsEnabled = true
);
