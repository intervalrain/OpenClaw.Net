namespace OpenClaw.Contracts.Skills;

public class ToolResult(bool isSuccess, string? output = null, string? error = null)
{
    public bool IsSuccess { get; private init; } = isSuccess;
    public string? Output { get; private init; } = output;
    public string? Error { get; private init; } = error;

    public static ToolResult Success(string output) => new(true, output);
    public static ToolResult Failure(string error) => new(false, error: error);
}