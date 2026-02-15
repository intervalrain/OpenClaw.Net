namespace OpenClaw.Contracts.Skills;

public class SkillResult(bool isSuccess, string? output = null, string? error = null)
{
    public bool IsSuccess { get; private init; } = isSuccess;
    public string? Output { get; private init; } = output;
    public string? Error { get; private init; } = error;

    public static SkillResult Success(string output) => new(true, output);
    public static SkillResult Failure(string error) => new(false, error: error);
}