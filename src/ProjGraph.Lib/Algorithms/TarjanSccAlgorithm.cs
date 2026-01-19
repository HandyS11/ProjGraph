using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Algorithms;

/// <summary>
/// Provides an implementation of Tarjan's algorithm to find strongly connected components (SCCs) in a directed graph.
/// </summary>
public static class TarjanSccAlgorithm
{
    /// <summary>
    /// Represents the context used during the execution of Tarjan's algorithm.
    /// </summary>
    private sealed class TarjanContext
    {
        /// <summary>
        /// Gets or sets the current index used to assign discovery times to nodes.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets the stack used to store nodes during the depth-first search.
        /// </summary>
        public Stack<Guid> Stack { get; } = new();

        /// <summary>
        /// Gets the dictionary that maps each node to its discovery index.
        /// </summary>
        public Dictionary<Guid, int> Indices { get; } = [];

        /// <summary>
        /// Gets the dictionary that maps each node to the smallest index reachable from that node.
        /// </summary>
        public Dictionary<Guid, int> Lowlink { get; } = [];

        /// <summary>
        /// Gets the set of nodes currently on the stack.
        /// </summary>
        public HashSet<Guid> OnStack { get; } = [];

        /// <summary>
        /// Gets the list of strongly connected components found during the algorithm execution.
        /// </summary>
        public List<List<Guid>> Sccs { get; } = [];
    }

    /// <summary>
    /// Finds all strongly connected components (SCCs) in the given solution or project graph.
    /// </summary>
    /// <param name="graph">The solution or project graph to analyze.</param>
    /// <returns>A read-only list of strongly connected components, where each component is a list of project IDs.</returns>
    public static IReadOnlyList<IReadOnlyList<Guid>> FindStronglyConnectedComponents(SolutionGraph graph)
    {
        var adjacencyList = graph.Projects.ToDictionary(
            p => p.Id,
            p => graph.Dependencies
                .Where(d => d.SourceId == p.Id)
                .Select(d => d.TargetId)
                .ToList()
        );

        var context = new TarjanContext();

        foreach (var v in graph.Projects
                     .Select(p => p.Id)
                     .Where(id => !context.Indices.ContainsKey(id)))
        {
            StrongConnect(v, adjacencyList, context);
        }

        return context.Sccs;
    }

    /// <summary>
    /// Recursively explores the graph to find strongly connected components using Tarjan's algorithm.
    /// </summary>
    /// <param name="v">The current node being visited.</param>
    /// <param name="adjacencyList">The adjacency list representing the graph.</param>
    /// <param name="context">The context of the Tarjan's algorithm execution.</param>
    private static void StrongConnect(
        Guid v,
        Dictionary<Guid, List<Guid>> adjacencyList,
        TarjanContext context)
    {
        context.Indices[v] = context.Index;
        context.Lowlink[v] = context.Index;
        context.Index++;
        context.Stack.Push(v);
        context.OnStack.Add(v);

        if (adjacencyList.TryGetValue(v, out var neighbors))
        {
            foreach (var w in neighbors)
            {
                if (!context.Indices.TryGetValue(w, out var index1))
                {
                    StrongConnect(w, adjacencyList, context);
                    context.Lowlink[v] = Math.Min(context.Lowlink[v], context.Lowlink[w]);
                }
                else if (context.OnStack.Contains(w))
                {
                    context.Lowlink[v] = Math.Min(context.Lowlink[v], index1);
                }
            }
        }

        // ReSharper disable once InvertIf
        if (context.Lowlink[v] == context.Indices[v])
        {
            var component = new List<Guid>();
            Guid w;
            do
            {
                w = context.Stack.Pop();
                context.OnStack.Remove(w);
                component.Add(w);
            } while (w != v);

            context.Sccs.Add(component);
        }
    }
}