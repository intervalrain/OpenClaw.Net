namespace OpenClaw.Infrastructure.Configuration;

public static class ConfigLoader
{
    public static Dictionary<string, string> LoadEnvFile(string? path)
    {
        var values = new Dictionary<string, string>(); 
        path ??= FindEnvFile();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return values;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0) continue;

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            // Remove surrounding quotes
            if (value.Length >= 2 && 
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }
        return values;
    }

    private static string? FindEnvFile()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var envPath = Path.Combine(dir, ".env");
            if (File.Exists(envPath)) return envPath;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

}