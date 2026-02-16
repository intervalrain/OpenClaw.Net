namespace OpenClaw.Contracts.Agents;

public enum AgentStreamEventType
{
    Thinking,
    ToolExecuting,
    ToolCompleted,
    ContentDelta,
    Completed,
    Error
}

public record AgentStreamEvent(
    AgentStreamEventType Type,
    string? Content = null,
    string? ToolName = null);