using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Domain.Algorithms;

namespace ProjGraph.Lib.ProjectGraph.Application.UseCases;

/// <summary>
/// Use case responsible for computing architectural statistics from a pre-built <see cref="SolutionGraph"/>.
/// All computation is pure, in-memory graph analysis — no I/O or parsing is performed here.
/// </summary>
public static class ComputeStatsUseCase
{
    private static readonly ProjectType[] KnownTypes =
    [
        ProjectType.Library,
        ProjectType.Executable,
        ProjectType.Test,
        ProjectType.Other
    ];

    /// <summary>
    /// Computes solution metrics from the given graph.
    /// </summary>
    /// <param name="graph">The pre-built solution graph to analyse.</param>
    /// <param name="topN">Number of top most-referenced projects to include in the hotspot list.</param>
    /// <returns>A <see cref="SolutionStats"/> snapshot.</returns>
    public static SolutionStats Execute(SolutionGraph graph, int topN = 5)
    {
        // Exclude NuGet package nodes — they are not projects
        var projects = graph.Projects.Where(p => p.Type != ProjectType.Package).ToList();

        var projectIds = projects.Select(p => p.Id).ToHashSet();
        var projectRefs = graph.Dependencies
            .Where(d => d.Type == DependencyType.ProjectReference
                        && projectIds.Contains(d.SourceId)
                        && projectIds.Contains(d.TargetId))
            .ToList();

        // 1. Type breakdown — all known types always present, even with count 0
        var typeBreakdown = KnownTypes.ToDictionary(
            t => t.ToString(),
            t => projects.Count(p => p.Type == t));

        // 2. Cycle detection — Tarjan SCC; any SCC with >1 member indicates a cycle
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);
        var hasCycles = sccs.Any(scc => scc.Count > 1);

        // 3. Dependency depth stats
        var depthStats = hasCycles
            ? new DependencyDepthStats(null, null, null)
            : ComputeDepths(projects, projectRefs);

        // 4. Hotspot ranking — direct in-degree (how many projects reference each project)
        var inDegrees = projects.ToDictionary(p => p.Id, _ => 0);
        foreach (var dep in projectRefs)
        {
            inDegrees[dep.TargetId]++;
        }

        var projectById = projects.ToDictionary(p => p.Id);
        var hotspots = inDegrees
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => new HotspotProject(projectById[kv.Key].Name, kv.Value))
            .ToList();

        return new SolutionStats(
            graph.Name,
            graph.Path,
            projects.Count,
            typeBreakdown.AsReadOnly(),
            depthStats,
            hotspots.AsReadOnly(),
            hasCycles);
    }

    /// <summary>
    /// Computes the longest-path depth for every project in the graph using Kahn's topological
    /// sort followed by a reverse-order dynamic programming pass.
    /// </summary>
    /// <param name="projects">The list of non-Package projects to analyse.</param>
    /// <param name="projectRefs">The list of project-to-project dependency edges.</param>
    /// <returns>A <see cref="DependencyDepthStats"/> containing average, min and max depth values.</returns>
    private static DependencyDepthStats ComputeDepths(
        List<Project> projects,
        List<Dependency> projectRefs)
    {
        if (projects.Count == 0)
        {
            return new DependencyDepthStats(0.0, 0, 0);
        }

        // adjacency: source → list of targets
        var adjacency = projects.ToDictionary(p => p.Id, _ => new List<Guid>());
        var inDegreeCount = projects.ToDictionary(p => p.Id, _ => 0);

        foreach (var dep in projectRefs)
        {
            adjacency[dep.SourceId].Add(dep.TargetId);
            inDegreeCount[dep.TargetId]++;
        }

        // Kahn's algorithm — produces topological order
        var queue = new Queue<Guid>(
            projects.Where(p => inDegreeCount[p.Id] == 0).Select(p => p.Id));
        var topoOrder = new List<Guid>(projects.Count);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            topoOrder.Add(node);
            foreach (var neighbour in adjacency[node].Where(neighbour => --inDegreeCount[neighbour] == 0))
            {
                queue.Enqueue(neighbour);
            }
        }

        // Longest-path DP in reverse topological order
        // depth[node] = max depth reachable from this node (0 for leaves)
        var depth = projects.ToDictionary(p => p.Id, _ => 0);
        for (var i = topoOrder.Count - 1; i >= 0; i--)
        {
            var node = topoOrder[i];
            foreach (var candidate in adjacency[node].Select(child => depth[child] + 1)
                         .Where(candidate => candidate > depth[node]))
            {
                depth[node] = candidate;
            }
        }

        var depths = depth.Values.ToList();
        var average = Math.Round(depths.Average(), 2);
        var min = depths.Min();
        var max = depths.Max();

        return new DependencyDepthStats(average, min, max);
    }
}
