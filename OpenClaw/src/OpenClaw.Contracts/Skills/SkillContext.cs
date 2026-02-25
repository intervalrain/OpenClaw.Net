namespace OpenClaw.Contracts.Skills;

public class SkillContext(string? arguments)
{
    public string? Arguments { get; init; } = arguments;
}