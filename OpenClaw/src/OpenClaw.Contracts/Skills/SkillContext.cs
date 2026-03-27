namespace OpenClaw.Contracts.Skills;

public class ToolContext(string? arguments)
{
    public string? Arguments { get; init; } = arguments;
}