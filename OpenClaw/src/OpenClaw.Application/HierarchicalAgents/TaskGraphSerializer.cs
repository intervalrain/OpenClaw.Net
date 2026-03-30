using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Serializes/deserializes TaskGraph to/from JSON.
/// Supports loading DAG definitions from workflow files.
/// </summary>
public static class TaskGraphSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static TaskGraph? DeserializeJson(string json)
    {
        var dto = JsonSerializer.Deserialize<TaskGraphDto>(json, Options);
        if (dto is null)
            return null;

        var nodes = dto.Nodes.Select(n => new TaskNode
        {
            Id = n.Id,
            AgentName = n.Agent,
            Input = n.Input is not null ? JsonDocument.Parse(JsonSerializer.Serialize(n.Input, Options)) : null
        }).ToList();

        var edges = dto.Edges.Select(e => new TaskEdge
        {
            FromNodeId = e.From,
            ToNodeId = e.To,
            OutputMapping = e.Mapping
        }).ToList();

        return new TaskGraph
        {
            Name = dto.Name,
            Nodes = nodes,
            Edges = edges
        };
    }

    public static string SerializeJson(TaskGraph graph)
    {
        var dto = new TaskGraphDto
        {
            Name = graph.Name,
            Nodes = graph.Nodes.Select(n => new TaskNodeDto
            {
                Id = n.Id,
                Agent = n.AgentName,
                Input = n.Input is not null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(n.Input.RootElement.GetRawText(), Options)
                    : null
            }).ToList(),
            Edges = graph.Edges.Select(e => new TaskEdgeDto
            {
                From = e.FromNodeId,
                To = e.ToNodeId,
                Mapping = e.OutputMapping
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, new JsonSerializerOptions(Options) { WriteIndented = true });
    }

    public static TaskGraph? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return DeserializeJson(json);
    }
}

internal record TaskGraphDto
{
    public string Name { get; init; } = "";
    public List<TaskNodeDto> Nodes { get; init; } = [];
    public List<TaskEdgeDto> Edges { get; init; } = [];
}

internal record TaskNodeDto
{
    public string Id { get; init; } = "";
    public string Agent { get; init; } = "";
    public Dictionary<string, object>? Input { get; init; }
}

internal record TaskEdgeDto
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string? Mapping { get; init; }
}
