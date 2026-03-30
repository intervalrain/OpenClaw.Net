namespace OpenClaw.Contracts.HierarchicalAgents;

public enum AgentExecutionType
{
    /// <summary>No LLM, pure deterministic logic.</summary>
    Deterministic,

    /// <summary>Wraps LLM call with tool-use loop.</summary>
    Llm,

    /// <summary>Deterministic with optional LLM fallback.</summary>
    Hybrid
}
