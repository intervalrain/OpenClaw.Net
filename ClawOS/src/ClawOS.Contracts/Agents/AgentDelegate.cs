namespace ClawOS.Contracts.Agents;

public delegate Task<string> AgentDelegate(AgentContext context, CancellationToken ct = default);