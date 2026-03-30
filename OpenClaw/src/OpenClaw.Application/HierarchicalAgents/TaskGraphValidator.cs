using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

public static class TaskGraphValidator
{
    /// <summary>
    /// Validates a TaskGraph: checks node uniqueness, edge reference integrity, and acyclicity.
    /// Returns a list of errors. Empty list means valid.
    /// </summary>
    public static List<string> Validate(TaskGraph graph)
    {
        var errors = new List<string>();

        if (graph.Nodes.Count == 0)
        {
            errors.Add("Graph must have at least one node.");
            return errors;
        }

        // Check unique node IDs
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in graph.Nodes)
        {
            if (!nodeIds.Add(node.Id))
                errors.Add($"Duplicate node ID: '{node.Id}'.");
        }

        // Check edge references
        foreach (var edge in graph.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId))
                errors.Add($"Edge references unknown source node: '{edge.FromNodeId}'.");
            if (!nodeIds.Contains(edge.ToNodeId))
                errors.Add($"Edge references unknown target node: '{edge.ToNodeId}'.");
            if (string.Equals(edge.FromNodeId, edge.ToNodeId, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Self-loop detected on node: '{edge.FromNodeId}'.");
        }

        // Check for cycles using topological sort (Kahn's algorithm)
        if (errors.Count == 0 && HasCycle(graph))
            errors.Add("Graph contains a cycle.");

        return errors;
    }

    /// <summary>
    /// Returns topologically sorted node IDs, or null if the graph has a cycle.
    /// </summary>
    public static List<string>? TopologicalSort(TaskGraph graph)
    {
        var inDegree = graph.Nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var adjacency = graph.Nodes.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var edge in graph.Edges)
        {
            adjacency[edge.FromNodeId].Add(edge.ToNodeId);
            inDegree[edge.ToNodeId]++;
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        return sorted.Count == graph.Nodes.Count ? sorted : null;
    }

    /// <summary>
    /// Returns the set of upstream node IDs for a given node (direct dependencies).
    /// </summary>
    public static HashSet<string> GetUpstreamNodes(TaskGraph graph, string nodeId)
    {
        return graph.Edges
            .Where(e => string.Equals(e.ToNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FromNodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the set of downstream node IDs for a given node (direct dependents).
    /// </summary>
    public static HashSet<string> GetDownstreamNodes(TaskGraph graph, string nodeId)
    {
        return graph.Edges
            .Where(e => string.Equals(e.FromNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.ToNodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasCycle(TaskGraph graph)
    {
        return TopologicalSort(graph) is null;
    }
}
