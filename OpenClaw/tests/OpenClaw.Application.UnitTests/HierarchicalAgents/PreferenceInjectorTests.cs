using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Repositories;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class PreferenceInjectorTests
{
    private const string BasePrompt = "You are a helpful assistant.";

    [Fact]
    public async Task EnrichWithPreferences_NoUserId_ReturnsOriginalPrompt()
    {
        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions(),
            UserId = null
        };

        var result = await PreferenceInjector.EnrichWithPreferencesAsync(BasePrompt, context);

        result.ShouldBe(BasePrompt);
    }

    [Fact]
    public async Task EnrichWithPreferences_NoRepository_ReturnsOriginalPrompt()
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IUserPreferenceRepository)).Returns(null as object);

        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = services,
            Options = new AgentExecutionOptions(),
            UserId = Guid.NewGuid()
        };

        var result = await PreferenceInjector.EnrichWithPreferencesAsync(BasePrompt, context);

        result.ShouldBe(BasePrompt);
    }

    [Fact]
    public async Task EnrichWithPreferences_NoPreferences_ReturnsOriginalPrompt()
    {
        var userId = Guid.NewGuid();
        var prefRepo = Substitute.For<IUserPreferenceRepository>();
        prefRepo.GetAllByUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserPreference>());

        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IUserPreferenceRepository)).Returns(prefRepo);

        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = services,
            Options = new AgentExecutionOptions(),
            UserId = userId
        };

        var result = await PreferenceInjector.EnrichWithPreferencesAsync(BasePrompt, context);

        result.ShouldBe(BasePrompt);
    }

    [Fact]
    public async Task EnrichWithPreferences_WithPreferences_AppendsSection()
    {
        var userId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            UserPreference.Create(userId, "language", "zh-TW"),
            UserPreference.Create(userId, "tone", "formal")
        };

        var prefRepo = Substitute.For<IUserPreferenceRepository>();
        prefRepo.GetAllByUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(preferences);

        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IUserPreferenceRepository)).Returns(prefRepo);

        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = services,
            Options = new AgentExecutionOptions(),
            UserId = userId
        };

        var result = await PreferenceInjector.EnrichWithPreferencesAsync(BasePrompt, context);

        result.ShouldStartWith(BasePrompt);
        result.ShouldContain("<user-preferences>");
        result.ShouldContain("- language: zh-TW");
        result.ShouldContain("- tone: formal");
        result.ShouldContain("</user-preferences>");
    }
}
