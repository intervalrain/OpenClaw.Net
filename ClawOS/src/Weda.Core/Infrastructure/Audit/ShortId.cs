using System.Security.Cryptography;

namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// Generates short unique IDs for tracing purposes.
/// Uses Base62 encoding (a-z, A-Z, 0-9) for URL-safe output.
/// </summary>
public static class ShortId
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";   
    private const int DefaultLength = 12;

    /// <summary>
    /// Generates a cryptographical random short ID.
    /// </summary>
    /// <param name="length">Length of ID (default: 12)</param>
    public static string Generate(int length = DefaultLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];

        for (int i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);    
    }
}