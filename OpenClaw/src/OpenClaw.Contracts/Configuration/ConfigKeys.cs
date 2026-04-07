namespace OpenClaw.Contracts.Configuration;

public class ConfigKeys
{
    public const string LlmProvider = "LLM_PROVIDER";
    public const string OpenAiModel = "OPENAI_MODEL";
    public const string OpenAiApiKey = "OPENAI_API_KEY";
    public const string OllamaUrl = "OLLAMA_URL";
    public const string OllamaModel = "OLLAMA_MODEL";
    public const string EncryptionKey = "OPENCLAW_ENCRYPTION_KEY";
    public const string GitHubToken = "GH_TOKEN";
    public const string AzureDevOpsPat = "AZURE_DEVOPS_PAT";
    public const string AzureDevOpsOrg = "AZURE_DEVOPS_ORG";
    public const string NotionApiToken = "NOTION_API_TOKEN";

    // Auto-update
    public const string AutoUpdateEnabled = "AUTO_UPDATE_ENABLED";
    public const string AutoUpdateCheckInterval = "AUTO_UPDATE_CHECK_INTERVAL";
    public const string AutoUpdateRepo = "AUTO_UPDATE_REPO";
    public const string LatestAvailableVersion = "LATEST_AVAILABLE_VERSION";
    public const string LastNotifiedVersion = "AUTO_UPDATE_LAST_NOTIFIED_VERSION";
    public const string UpdateStatus = "AUTO_UPDATE_STATUS";
    public const string UpdateStatusMessage = "AUTO_UPDATE_STATUS_MESSAGE";
}