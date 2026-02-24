using Weda.Core.Domain;

namespace OpenClaw.Domain.Skills.Entities;

public class SkillSetting : Entity<Guid>
{
    public string SkillName { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; } = true;

    private SkillSetting() : base(Guid.NewGuid()) { }

    public static SkillSetting Create(string skillName, bool isEnabled = true)
    {
        return new SkillSetting
        {
            SkillName = skillName,
            IsEnabled = isEnabled,
        };
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}