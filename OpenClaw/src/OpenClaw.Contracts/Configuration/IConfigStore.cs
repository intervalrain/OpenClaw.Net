namespace OpenClaw.Contracts.Configuration;

public interface IConfigStore
{
    string? Get(string key);
    string GetRequired(string key);
    T? Get<T>(string key) where T : class;
}