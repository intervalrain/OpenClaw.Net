namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// Generates short, human-friendly RequestId for logging and diagnostics.
/// </summary>
public static class RequestIdGenerator
{
    private const int DefaultLength = 16;

    public static string Generate(int length = DefaultLength)
        => ShortId.Generate(length);
}
