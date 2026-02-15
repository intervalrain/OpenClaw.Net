using OllamaSharp;

using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Skills;
using OpenClaw.Infrastructure.Llm.Ollama;
using OpenClaw.Skills.FileSystem.ListDirectory;
using OpenClaw.Skills.FileSystem.ReadFile;
using OpenClaw.Skills.FileSystem.WriteFile;
using OpenClaw.Skills.Shell.ExecuteCommand;
using OpenClaw.Skills.Http.HttpRequest;

var client = new OllamaApiClient("http://localhost:11434");
var llmProvider = new OllamaLlmProvider(client, "qwen2.5:7b");

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