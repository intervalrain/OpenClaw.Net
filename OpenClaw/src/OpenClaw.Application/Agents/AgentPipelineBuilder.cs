using Microsoft.Extensions.DependencyInjection;

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
        IEnumerable<IAgentSkill> skills,
        AgentPipelineOptions options)
    {
        return new AgentPipeline(llmProviderFactory, skills, options, _middlewares);
    }
}