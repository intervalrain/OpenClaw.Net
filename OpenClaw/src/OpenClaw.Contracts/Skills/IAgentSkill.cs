namespace OpenClaw.Contracts.Skills;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    object? Parameters { get; }
    Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default);
}