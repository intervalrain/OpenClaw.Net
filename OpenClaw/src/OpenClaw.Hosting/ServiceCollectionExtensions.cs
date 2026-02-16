using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Application.Agents.Middlewares;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Infrastructure.Configuration;
using OpenClaw.Infrastructure.Llm.Ollama;
using OpenClaw.Infrastructure.Llm.OpenAI;
using OpenClaw.Skills.FileSystem.ListDirectory;
using OpenClaw.Skills.FileSystem.ReadFile;
using OpenClaw.Skills.FileSystem.WriteFile;
using OpenClaw.Skills.Http.HttpRequest;
using OpenClaw.Skills.Shell.ExecuteCommand;

using Serilog;

namespace OpenClaw.Hosting;

public static class ServiceCollectionExtensions
{
    // Logging
    public static IServiceCollection AddOpenClaw(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        // Logging
        services.AddLogging(builder => builder.AddSerilog());

        // Middlewares
        services.AddSingleton<LoggingMiddleware>();
        services.AddSingleton<ErrorHandlingMiddleware>();
        services.AddSingleton<TimeoutMiddleware>();

        // ConfigStore
        services.AddSingleton<IConfigStore, EnvironmentConfigStore>();

        // LLM Providers
        services.AddKeyedSingleton<ILlmProvider, OpenAILlmProvider>("openai");
        services.AddKeyedSingleton<ILlmProvider, OllamaLlmProvider>("ollama");

        // skills
        services.AddSingleton<IAgentSkill>(ReadFileSkill.Default);
        services.AddSingleton<IAgentSkill>(WriteFileSkill.Default);
        services.AddSingleton<IAgentSkill>(ListDirectorySkill.Default);
        services.AddSingleton<IAgentSkill>(ExecuteCommandSkill.Default);
        services.AddSingleton<IAgentSkill>(HttpRequestSkill.Default);

        return services;
    }
}
