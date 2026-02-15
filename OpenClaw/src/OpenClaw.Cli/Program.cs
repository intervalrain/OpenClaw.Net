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

var config = new EnvironmentConfigStore();
ILlmProvider llmProvider = config.Get(ConfigKeys.LlmProvider) switch 
{
    "openai" => new OpenAILlmProvider(config),
    _ => new OllamaLlmProvider(config)
};


var skills = new IAgentSkill[]
{
    ReadFileSkill.Default,
    WriteFileSkill.Default,
    ListDirectorySkill.Default,
    ExecuteCommandSkill.Default,
    HttpRequestSkill.Default,
};

var options = new AgentPipelineOptions
{
    SystemPrompt = "You are a helpful assistant. Use tools when needed",
    MaxIterations = 5,
};

var pipeline = new AgentPipeline(llmProvider, skills, options);

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