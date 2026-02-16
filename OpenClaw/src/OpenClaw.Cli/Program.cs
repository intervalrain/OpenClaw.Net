using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Skills;
using OpenClaw.Skills.FileSystem.ListDirectory;
using OpenClaw.Skills.FileSystem.ReadFile;
using OpenClaw.Skills.FileSystem.WriteFile;
using OpenClaw.Skills.Shell.ExecuteCommand;
using OpenClaw.Skills.Http.HttpRequest;
using OpenClaw.Infrastructure.Configuration;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Infrastructure.Llm.OpenAI;
using OpenClaw.Infrastructure.Llm.Ollama;
using OpenClaw.Contracts.Llm;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Application.Agents.Middlewares;
using Microsoft.Extensions.Options;

var services = new ServiceCollection();

// logging
services.AddLogging(builder => builder.AddConsole());

// middlewares
services.AddSingleton<LoggingMiddleware>();
services.AddSingleton<ErrorHandlingMiddleware>();
services.AddSingleton<TimeoutMiddleware>();

// config store
services.AddSingleton<IConfigStore, EnvironmentConfigStore>();

// llm
services.AddKeyedSingleton<ILlmProvider, OpenAILlmProvider>("openai");
services.AddKeyedSingleton<ILlmProvider, OllamaLlmProvider>("ollama");

// skills
services.AddSingleton<IAgentSkill>(ReadFileSkill.Default);
services.AddSingleton<IAgentSkill>(WriteFileSkill.Default);
services.AddSingleton<IAgentSkill>(ListDirectorySkill.Default);
services.AddSingleton<IAgentSkill>(ExecuteCommandSkill.Default);
services.AddSingleton<IAgentSkill>(HttpRequestSkill.Default);

// agent pipeline
services.Configure<AgentPipelineOptions>(opts =>
{
    opts.MaxIterations = 5;
    opts.SystemPrompt = "You are a helpful assistant. Use tools when needed";
});
var sp = services.BuildServiceProvider();
var config = sp.GetRequiredService<IConfigStore>();
var llmProvider = sp.GetRequiredKeyedService<ILlmProvider>(config.Get(ConfigKeys.LlmProvider) ?? "ollama");
var skills = sp.GetServices<IAgentSkill>();
var options = sp.GetRequiredService<IOptions<AgentPipelineOptions>>().Value;

var pipeline = new AgentPipelineBuilder(sp)
    .Use<ErrorHandlingMiddleware>()
    .Use<LoggingMiddleware>()
    .Use<TimeoutMiddleware>()
    .Build(llmProvider, skills, options);

Console.WriteLine("OpenClaw CLI (type 'exit' to quit)");
Console.WriteLine("==================================");

while (true)
{
    Console.Write("\n> ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    try
    {
        var response = await pipeline.ExecuteAsync(input);
        Console.WriteLine($"\n{response}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
}