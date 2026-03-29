using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Security;
using OpenClaw.Infrastructure.Configuration;

namespace OpenClaw.Infrastructure.Security;

public class AesEncryptionService(ILogger<AesEncryptionService> logger) : IEncryptionService
{
    private byte[]? _key;

    private byte[] Key => _key ??= InitializeKey();

    private byte[] InitializeKey()
    {
        // Read directly from environment (encryption key should not be in DB)
        var envStore = new EnvironmentConfigStore();
        var keyString = envStore.Get(ConfigKeys.EncryptionKey);

        if (string.IsNullOrEmpty(keyString))
        {
            // In development, auto-generate for convenience
            var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            if (isDev)
            {
                keyString = GenerateAndPersistKey();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Encryption key is not configured. Set the '{ConfigKeys.EncryptionKey}' environment variable " +
                    "with a Base64-encoded 32-byte key. Generate one with: " +
                    "dotnet run -e \"Console.WriteLine(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)))\"");
            }
        }

        var key = Convert.FromBase64String(keyString);

        if (key.Length != 32)
        {
            throw new InvalidOperationException("Encryption key must be 32 bytes (256 bits) long.");
        }

        return key;
    }

    private string GenerateAndPersistKey()
    {
        var newKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        ConfigLoader.AppendToEnvFile(ConfigKeys.EncryptionKey, newKey);
        logger.LogWarning("Auto-generated encryption key (development only). For production, pre-generate and set via environment variable.");

        // Set for current process
        Environment.SetEnvironmentVariable(ConfigKeys.EncryptionKey, newKey);

        return newKey;
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var result = new byte[aes.IV.Length + ciphertextBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return string.Empty;

        var fullCipher = Convert.FromBase64String(ciphertext);

        using var aes = Aes.Create();
        aes.Key = Key;

        var iv = new byte[16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        aes.IV = iv;

        var ciphertextBytes = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, iv.Length, ciphertextBytes, 0, ciphertextBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
