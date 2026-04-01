namespace ClawOS.Contracts.Skills;

/// <summary>
/// Specifies the ConfigStore or UserPreference key to use as a default value for this parameter.
/// The suggest API will look up this key in ConfigStore first, then UserPreference.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DefaultFromAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
