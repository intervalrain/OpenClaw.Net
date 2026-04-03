namespace OpenClaw.Contracts.CronJobs;

public enum CronJobStreamEventType
{
    ToolCall,
    ToolResult,
    Content,
    Completed,
    Failed
}

public record CronJobStreamEvent(
    CronJobStreamEventType Type,
    string? ToolName = null,
    string? Arguments = null,
    string? Content = null);
