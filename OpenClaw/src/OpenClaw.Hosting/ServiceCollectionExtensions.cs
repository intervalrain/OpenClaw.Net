using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenClaw.Application.AgentActivities;
using OpenClaw.Application.Agents;
using OpenClaw.Application.Agents.Middlewares;
using OpenClaw.Application.CronJobs;
using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Application.Llm;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Application.Skills;
using OpenClaw.Channels.Telegram.Extensions;
using OpenClaw.Domain.AgentActivities.Repositories;
using OpenClaw.Domain.CronJobs.Repositories;
using OpenClaw.Infrastructure.AgentActivities.Persistence;
using OpenClaw.Infrastructure.Configuration;
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

        // HttpClient for local/dev scenarios — SSL bypass only when ASPNETCORE_ENVIRONMENT=Development
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        if (isDev)
        {
            services.AddHttpClient("SkipSslValidation")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });
        }
        else
        {
            services.AddHttpClient("SkipSslValidation");
        }

        // Middlewares
        services.AddSingleton<LoggingMiddleware>();
        services.AddSingleton<ErrorHandlingMiddleware>();
        services.AddSingleton<TimeoutMiddleware>();
        services.AddSingleton<SecretRedactionMiddleware>();

        // LLM Providers (keyed) - use EnvironmentConfigStore for startup config
        services.AddKeyedSingleton<ILlmProvider>("openai", (sp, _) =>
        {
            var envConfig = sp.GetRequiredService<EnvironmentConfigStore>();
            return new OpenAILlmProvider(envConfig);
        });
        services.AddKeyedSingleton<ILlmProvider>("ollama", (sp, _) =>
        {
            var envConfig = sp.GetRequiredService<EnvironmentConfigStore>();
            return new OllamaLlmProvider(envConfig);
        });

        // LLM Provider Factories (keyed) - for dynamic creation with custom params
        services.AddKeyedSingleton<Func<string, string, ILlmProvider>>("ollama",
            (_, _) => (url, model) => new OllamaLlmProvider(url, model));
        services.AddKeyedSingleton<Func<string, string, ILlmProvider>>("openai",
            (_, _) => (apiKey, model) => new OpenAILlmProvider(apiKey, model));

        // Default LLM Provider (resolved from config)
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var envConfig = sp.GetRequiredService<EnvironmentConfigStore>();
            var providerKey = envConfig.Get(ConfigKeys.LlmProvider) ?? "ollama";
            return sp.GetRequiredKeyedService<ILlmProvider>(providerKey);
        });

        // LLM Provider Factory
        services.AddScoped<ILlmProviderFactory, LlmProviderFactory>();

        // skills
        services.AddSkillsFromAssemblies(configuration);
        services.AddScoped<IToolSettingsService, ToolSettingsService>();
        services.AddSingleton<ISlashCommandParser, SlashCommandParser>();
        // services.AddSingleton<IAgentTool>(ReadFileSkill.Default);
        // services.AddSingleton<IAgentTool>(WriteFileSkill.Default);
        // services.AddSingleton<IAgentTool>(ListDirectorySkill.Default);
        // services.AddSingleton<IAgentTool>(ExecuteCommandSkill.Default);
        // services.AddSingleton<IAgentTool>(HttpRequestSkill.Default);
        // services.AddSingleton<IAgentTool>(WebSearchSkill.Default);

        // pipeline (Scoped to allow dynamic provider switching per request)
        services.AddScoped<IAgentPipeline>(sp =>
        {
            var llmProviderFactory = sp.GetRequiredService<ILlmProviderFactory>();
            var skills = sp.GetServices<IAgentTool>();
            var options = sp.GetRequiredService<IOptions<AgentPipelineOptions>>().Value;

            var pipeline = new AgentPipelineBuilder(sp)
                .Use<ErrorHandlingMiddleware>()
                .Use<SecretRedactionMiddleware>()  // Redact secrets before logging
                .Use<LoggingMiddleware>()
                .Use<TimeoutMiddleware>()
                .Build(llmProviderFactory, skills, options);

            return pipeline;
        });

        // Hierarchical Agent Architecture
        services.AddSingleton<IAgent, PioneerAgent>();
        services.AddAgentRegistry();
        services.AddScoped<IExecutionEngine, SimpleExecutionEngine>();
        services.AddScoped<IDagExecutor, DagExecutor>();
        services.AddScoped<DagExecutionEngine>();

        // Channels
        services.AddTelegramChannel(configuration);

        // Agent Activities
        services.AddScoped<IAgentActivityRepository, AgentActivityRepository>();
        services.AddSingleton<IAgentActivityBroadcast, AgentActivityBroadcast>();
        services.AddSingleton<IAgentActivityTracker, AgentActivityTracker>();

        // Cron Jobs
        services.AddSingleton<ICronJobExecutor, CronJobExecutor>();
        services.AddScoped<ICronJobRepository, OpenClaw.Infrastructure.CronJobs.Persistence.CronJobRepository>();
        services.AddScoped<ICronJobExecutionRepository, OpenClaw.Infrastructure.CronJobs.Persistence.CronJobExecutionRepository>();
        services.AddScoped<IToolInstanceRepository, OpenClaw.Infrastructure.CronJobs.Persistence.ToolInstanceRepository>();

        return services;
    }

    private static IServiceCollection AddSkillsFromAssemblies(this IServiceCollection services, IConfiguration configuration)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("OpenClaw.Tools") == true)
            .ToList();

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var skillDlls = Directory.GetFiles(basePath, "OpenClaw.Tools.*.dll");
        
        foreach (var dll in skillDlls)
        {
            var assemblyName = AssemblyName.GetAssemblyName(dll);
            if (!assemblies.Any(a => a.GetName().Name == assemblyName.Name))
            {
                assemblies.Add(Assembly.Load(assemblyName));
            }
        }

        var allTypes = assemblies.SelectMany(a => a.GetTypes()).ToList();

        // Register skill dependencies (classes in skill assemblies that are not skills themselves)
        // Exclude: records (Args, Result types), interfaces, abstract classes
        var skillDependencyTypes = allTypes
            .Where(t => !typeof(IAgentTool).IsAssignableFrom(t)
                && !t.IsInterface && !t.IsAbstract
                && !IsRecordType(t)  // Exclude records (Args, Result, etc.)
                && t.GetConstructors().Any(c => c.GetParameters().Length > 0));

        foreach (var depType in skillDependencyTypes)
        {
            if (!services.Any(s => s.ServiceType == depType))
            {
                services.AddSingleton(depType);
            }
        }

        // Register skills
        var skillTypes = allTypes
            .Where(t => typeof(IAgentTool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var skillType in skillTypes)
        {
            var defaultProperty = skillType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);

            if (defaultProperty != null && typeof(IAgentTool).IsAssignableFrom(defaultProperty.PropertyType))
            {
                if (defaultProperty.GetValue(null) is IAgentTool skillInstance)
                {
                    services.AddSingleton<IAgentTool>(skillInstance);
                    continue;
                }
            }

            services.AddSingleton(typeof(IAgentTool), skillType);
        }

        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // Register Markdown Skill store
        var skillsDir = configuration["Skills:Directory"] ?? "skills";
        if (!Path.IsPathRooted(skillsDir))
        {
            // Relative paths resolve from the content root (where the .csproj is)
            skillsDir = Path.GetFullPath(skillsDir, Directory.GetCurrentDirectory());
        }
        services.AddSingleton<ISkillStore>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileSkillStore>>();
            var store = new FileSkillStore(skillsDir, logger);
            store.ReloadAsync().GetAwaiter().GetResult();
            return store;
        });

        return services;
    }

    private static IServiceCollection AddAgentRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IAgentRegistry>(sp =>
        {
            // Collect explicitly registered IAgent instances
            var agents = sp.GetServices<IAgent>().ToList();

            // Wrap all IAgentTool instances as ToolAgents for backward compatibility
            var tools = sp.GetServices<IAgentTool>();
            foreach (var tool in tools)
            {
                agents.Add(new ToolAgent(tool));
            }

            return new AgentRegistry(agents);
        });

        return services;
    }

    private static bool IsRecordType(Type type)
    {
        // Records have a compiler-generated <Clone>$ method
        return type.GetMethod("<Clone>$") != null;
    }
}
