using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenClaw.Application.Agents;
using OpenClaw.Application.Agents.Middlewares;
using OpenClaw.Application.Llm;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Application.Skills;
using OpenClaw.Infrastructure.Configuration;
using OpenClaw.Channels.Telegram;
using OpenClaw.Infrastructure.Llm.Ollama;
using OpenClaw.Infrastructure.Llm.OpenAI;

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

        // HttpClient with SSL validation bypass (for local/dev scenarios)
        services.AddHttpClient("SkipSslValidation")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        // Middlewares
        services.AddSingleton<LoggingMiddleware>();
        services.AddSingleton<ErrorHandlingMiddleware>();
        services.AddSingleton<TimeoutMiddleware>();
        services.AddSingleton<SecretRedactionMiddleware>();

        // ConfigStore
        services.AddSingleton<IConfigStore, EnvironmentConfigStore>();

        // LLM Providers (keyed)
        services.AddKeyedSingleton<ILlmProvider, OpenAILlmProvider>("openai");
        services.AddKeyedSingleton<ILlmProvider, OllamaLlmProvider>("ollama");

        // LLM Provider Factories (keyed) - for dynamic creation with custom params
        services.AddKeyedSingleton<Func<string, string, ILlmProvider>>("ollama",
            (_, _) => (url, model) => new OllamaLlmProvider(url, model));
        services.AddKeyedSingleton<Func<string, string, ILlmProvider>>("openai",
            (_, _) => (apiKey, model) => new OpenAILlmProvider(apiKey, model));

        // Default LLM Provider (resolved from config)
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var configStore = sp.GetRequiredService<IConfigStore>();
            var providerKey = configStore.Get(ConfigKeys.LlmProvider) ?? "ollama";
            return sp.GetRequiredKeyedService<ILlmProvider>(providerKey);
        });

        // LLM Provider Factory
        services.AddScoped<ILlmProviderFactory, LlmProviderFactory>();

        // skills
        services.AddSkillsFromAssemblies();
        services.AddScoped<ISkillSettingsService, SkillSettingsService>();
        services.AddSingleton<ISlashCommandParser, SlashCommandParser>();
        // services.AddSingleton<IAgentSkill>(ReadFileSkill.Default);
        // services.AddSingleton<IAgentSkill>(WriteFileSkill.Default);
        // services.AddSingleton<IAgentSkill>(ListDirectorySkill.Default);
        // services.AddSingleton<IAgentSkill>(ExecuteCommandSkill.Default);
        // services.AddSingleton<IAgentSkill>(HttpRequestSkill.Default);
        // services.AddSingleton<IAgentSkill>(WebSearchSkill.Default);

        // Channel Adapters
        services.AddTelegramChannel(configuration);

        // pipeline (Scoped to allow dynamic provider switching per request)
        services.AddScoped<IAgentPipeline>(sp =>
        {
            var llmProviderFactory = sp.GetRequiredService<ILlmProviderFactory>();
            var skills = sp.GetServices<IAgentSkill>();
            var options = sp.GetRequiredService<IOptions<AgentPipelineOptions>>().Value;

            var pipeline = new AgentPipelineBuilder(sp)
                .Use<ErrorHandlingMiddleware>()
                .Use<SecretRedactionMiddleware>()  // Redact secrets before logging
                .Use<LoggingMiddleware>()
                .Use<TimeoutMiddleware>()
                .Build(llmProviderFactory, skills, options);

            return pipeline;
        });

        return services;
    }

    private static IServiceCollection AddSkillsFromAssemblies(this IServiceCollection services)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("OpenClaw.Skills") == true)
            .ToList();

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var skillDlls = Directory.GetFiles(basePath, "OpenClaw.Skills.*.dll");
        
        foreach (var dll in skillDlls)
        {
            var assemblyName = AssemblyName.GetAssemblyName(dll);
            if (!assemblies.Any(a => a.GetName().Name == assemblyName.Name))
            {
                assemblies.Add(Assembly.Load(assemblyName));
            }
        }

        var skillTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IAgentSkill).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        
        foreach (var skillType in skillTypes)
        {
            var defaultProperty = skillType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);

            if (defaultProperty != null && typeof(IAgentSkill).IsAssignableFrom(defaultProperty.PropertyType))
            {
                if (defaultProperty.GetValue(null) is IAgentSkill skillInstance)
                {
                    services.AddSingleton<IAgentSkill>(skillInstance);
                    continue;
                }
            }

            services.AddSingleton(typeof(IAgentSkill), skillType);
        }

        services.AddSingleton<ISkillRegistry, SkillRegistry>();

        return services;   
    }
}
