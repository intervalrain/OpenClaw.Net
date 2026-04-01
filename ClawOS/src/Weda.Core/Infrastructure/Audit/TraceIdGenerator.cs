using System.Security.Cryptography;
using System.Text;

namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// Generates W3C-compliant TraceId (128-bit, 32 lowercase hex chars)
/// </summary>
public static class TraceIdGenerator
{
    private const int TraceIdBytesLength = 16; // 128-bit

    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[TraceIdBytesLength];

        do
        {
            RandomNumberGenerator.Fill(bytes);
        }
        // W3C spec: trace-id MUST NOT be all zeros
        while (IsAllZero(bytes));

        return ToLowerHex(bytes);
    }

    private static bool IsAllZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (b != 0)
                return false;
        }
        return true;
    }

    private static string ToLowerHex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
