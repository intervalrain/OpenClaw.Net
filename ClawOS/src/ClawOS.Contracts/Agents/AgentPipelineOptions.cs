namespace ClawOS.Contracts.Agents;

public class AgentPipelineOptions
{
    public string? SystemPrompt { get; set; }
    public int MaxIterations { get; set; } = 10;
}