namespace OpenClaw.Contracts.Skills;

/// <summary>
/// Optional interface for tools that support streaming progress updates.
/// Tools implementing this will have their progress events forwarded to the client
/// during execution (e.g., shell command output, download progress).
///
/// Tools that only implement IAgentTool will continue to work as before (blocking).
/// </summary>
public interface IStreamingAgentTool : IAgentTool
{
    IAsyncEnumerable<ToolProgress> ExecuteStreamAsync(ToolContext context, CancellationToken ct = default);
}
