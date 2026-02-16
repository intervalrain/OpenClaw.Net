using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenClaw.Application.Agents;
using OpenClaw.Application.Agents.Middlewares;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Hosting;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddOpenClaw(configuration);

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