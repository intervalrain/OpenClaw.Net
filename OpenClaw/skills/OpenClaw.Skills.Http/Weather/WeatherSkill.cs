using System.ComponentModel;

using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Contracts.Skills;
using OpenClaw.Skills.Http.HttpRequest;

namespace OpenClaw.Skills.Http.Weather;

public class WeatherSkill(IServiceProvider sp) : AgentSkillBase<WeatherSkillArgs>
{
    public override string Name => "weather";
    public override string Description => "Get current weather information via wttr.in. Use when: user ask about weather, temperature, or forecasts for any location. ";

    public override async Task<SkillResult> ExecuteAsync(WeatherSkillArgs args, CancellationToken ct)
    {
        var registry = sp.GetRequiredService<ISkillRegistry>();
        var httpSkill = registry.GetSkill<HttpRequestSkill>()
            ?? throw new InvalidOperationException("No skill named 'http_request' has been registered");

        var url = args.Days switch
        {
            "0" => $"https://wttr.in/{args.Location}?0",
            "1" => $"https://wttr.in/{args.Location}?1", 
            _ => $"https://wttr.in/{args.Location}?format=3",
        };

        var result = await httpSkill.ExecuteAsync(new HttpRequestArgs(url, "GET", ""), ct);

        return result;
    }
}

public record WeatherSkillArgs(
    [property: Description("The location to get weather for (e.g. 'Taipei', 'New York', 'London').")]
    string? Location,

    [property: Description("Forecast days: '0' for current, '1' for tomorrow, or leave empty for 3-day forecast.")]
    string? Days
);