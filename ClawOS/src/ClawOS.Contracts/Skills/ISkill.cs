namespace ClawOS.Contracts.Skills;

/// <summary>
/// A Skill is a high-level capability defined in Markdown (SKILL.md).
/// It bundles system prompt instructions with a list of available Tools.
/// The LLM uses these instructions + tools to accomplish the task.
/// </summary>
public interface ISkill
{
    /// <summary>Unique skill name (e.g., "daily-ado-report").</summary>
    string Name { get; }

    /// <summary>Brief description of what this skill does and when to use it.</summary>
    string Description { get; }

    /// <summary>System prompt / instructions for the LLM when executing this skill.</summary>
    string Instructions { get; }

    /// <summary>List of tool names this skill can use (e.g., ["azure_devops", "git"]).</summary>
    IReadOnlyList<string> Tools { get; }
}
