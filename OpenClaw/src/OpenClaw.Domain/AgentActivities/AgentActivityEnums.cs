namespace OpenClaw.Domain.AgentActivities;

/// <summary>
/// The type of agent activity being performed.
/// </summary>
public enum ActivityType
{
    /// <summary>
    /// Interactive chat session with a user.
    /// </summary>
    Chat,

    /// <summary>
    /// Scheduled cron job execution.
    /// </summary>
    CronJob,

    /// <summary>
    /// Tool/skill invocation within an agent pipeline.
    /// </summary>
    ToolExecution
}

/// <summary>
/// The current status of an agent activity.
/// </summary>
public enum ActivityStatus
{
    /// <summary>
    /// Activity has started.
    /// </summary>
    Started,

    /// <summary>
    /// Agent is thinking / waiting for LLM response.
    /// </summary>
    Thinking,

    /// <summary>
    /// Agent is executing a tool/skill.
    /// </summary>
    ToolExecuting,

    /// <summary>
    /// Activity completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Activity failed with an error.
    /// </summary>
    Failed
}
