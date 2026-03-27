using System.Text.Json.Serialization;

namespace OpenClaw.Contracts.Workflows;

/// <summary>
/// Configuration for workflow scheduling (cron job).
/// </summary>
public record ScheduleConfig
{
    /// <summary>
    /// Whether the schedule is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Frequency of execution.
    /// </summary>
    public ScheduleFrequency Frequency { get; init; }

    /// <summary>
    /// Time of day to execute (in the specified timezone).
    /// </summary>
    public TimeOnly TimeOfDay { get; init; }

    /// <summary>
    /// Days of week for Weekly frequency. Required when Frequency is Weekly.
    /// </summary>
    public DayOfWeek[]? DaysOfWeek { get; init; }

    /// <summary>
    /// Day of month for Monthly frequency. Required when Frequency is Monthly.
    /// </summary>
    public int? DayOfMonth { get; init; }

    /// <summary>
    /// Timezone for schedule evaluation. Defaults to "UTC".
    /// Uses IANA timezone IDs (e.g., "Asia/Taipei", "America/New_York").
    /// </summary>
    public string Timezone { get; init; } = "UTC";

    /// <summary>
    /// Last successful execution time (for tracking).
    /// </summary>
    public DateTime? LastExecutedAt { get; init; }
}

/// <summary>
/// Frequency options for workflow scheduling.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScheduleFrequency>))]
public enum ScheduleFrequency
{
    Daily,
    Weekly,
    Monthly
}