using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenClaw.Application.Agents.ContextProviders;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Agents;

public class AgentPipelineBuilder(IServiceProvider _serviceProvider)
{
    private readonly List<IAgentMiddleware> _middlewares = [];

    public AgentPipelineBuilder Use(IAgentMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    public AgentPipelineBuilder Use<T>() where T : IAgentMiddleware
    {
        _middlewares.Add(_serviceProvider.GetRequiredService<T>());
        return this;
    }

    public IAgentPipeline Build(
        ILlmProviderFactory llmProviderFactory,
        IEnumerable<IAgentTool> skills,
        AgentPipelineOptions options)
    {
        var skillStore = _serviceProvider.GetService<ISkillStore>();
        var logger = _serviceProvider.GetService<ILogger<AgentPipeline>>();
        var assembler = _serviceProvider.GetService<ContextProviders.SystemPromptAssembler>();
        return new AgentPipeline(llmProviderFactory, skills, options, skillStore, _middlewares, logger, assembler);
    }
}