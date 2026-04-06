namespace OpenClaw.Contracts.Agents;

public class AgentPipelineOptions
{
    public string? SystemPrompt { get; set; }
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// When total tool count exceeds this threshold, only core tools + tool_search
    /// are sent to the LLM. The LLM discovers other tools via tool_search on demand.
    /// Set to 0 to disable deferred loading. Default: 15.
    /// </summary>
    public int DeferredToolThreshold { get; set; } = 15;
}