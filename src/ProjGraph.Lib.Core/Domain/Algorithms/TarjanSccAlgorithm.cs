using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Core.Domain.Algorithms;

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
    /// Represents a single frame on the explicit DFS stack used by the iterative Tarjan implementation.
    /// </summary>
    /// <param name="node">The graph node this frame represents.</param>
    /// <param name="neighborIndex">The index into the node's neighbor list to resume iteration from.</param>
    private readonly struct StackFrame(Guid node, int neighborIndex)
    {
        public Guid Node { get; } = node;
        public int NeighborIndex { get; } = neighborIndex;
    }

    /// <summary>
    /// Iteratively explores the graph to find strongly connected components using Tarjan's algorithm.
    /// Uses an explicit stack instead of recursion to avoid <see cref="StackOverflowException"/>
    /// on deep dependency chains.
    /// </summary>
    /// <param name="v">The starting node.</param>
    /// <param name="adjacencyList">The adjacency list representing the graph.</param>
    /// <param name="context">The context of the Tarjan's algorithm execution.</param>
    private static void StrongConnect(
        Guid v,
        Dictionary<Guid, List<Guid>> adjacencyList,
        TarjanContext context)
    {
        var dfsStack = new Stack<StackFrame>();

        // Initialize the starting node
        context.Indices[v] = context.Index;
        context.Lowlink[v] = context.Index;
        context.Index++;
        context.Stack.Push(v);
        context.OnStack.Add(v);

        dfsStack.Push(new StackFrame(v, 0));

        while (dfsStack.Count > 0)
        {
            var frame = dfsStack.Pop();
            var node = frame.Node;

            if (TryPushNeighbor(node, frame.NeighborIndex, adjacencyList, dfsStack, context))
            {
                continue;
            }

            // All neighbors processed — check for SCC root
            if (context.Lowlink[node] == context.Indices[node])
            {
                CollectSccComponent(node, context);
            }

            // Update parent's lowlink
            if (dfsStack.Count > 0)
            {
                var parent = dfsStack.Peek();
                context.Lowlink[parent.Node] = Math.Min(context.Lowlink[parent.Node], context.Lowlink[node]);
            }
        }
    }

    /// <summary>
    /// Iterates over the neighbors of <paramref name="node"/> starting at <paramref name="neighborIdx"/>,
    /// and pushes the first unvisited neighbor onto the DFS stack.
    /// </summary>
    /// <param name="node">The node whose neighbors to iterate.</param>
    /// <param name="neighborIdx">The index into the neighbor list to resume iteration from.</param>
    /// <param name="adjacencyList">The adjacency list representing the graph.</param>
    /// <param name="dfsStack">The explicit DFS stack used by the iterative algorithm.</param>
    /// <param name="context">The context of the Tarjan's algorithm execution.</param>
    /// <returns><see langword="true"/> if an unvisited neighbor was pushed; otherwise <see langword="false"/>.</returns>
    private static bool TryPushNeighbor(
        Guid node,
        int neighborIdx,
        Dictionary<Guid, List<Guid>> adjacencyList,
        Stack<StackFrame> dfsStack,
        TarjanContext context)
    {
        var neighbors = adjacencyList.TryGetValue(node, out var list) ? list : [];

        for (var i = neighborIdx; i < neighbors.Count; i++)
        {
            var w = neighbors[i];
            if (!context.Indices.TryGetValue(w, out var wIndex))
            {
                // Save current frame (will resume at neighbor i+1 after w completes)
                dfsStack.Push(new StackFrame(node, i + 1));

                // Initialize w and push it
                context.Indices[w] = context.Index;
                context.Lowlink[w] = context.Index;
                context.Index++;
                context.Stack.Push(w);
                context.OnStack.Add(w);

                dfsStack.Push(new StackFrame(w, 0));
                return true;
            }

            if (context.OnStack.Contains(w))
            {
                context.Lowlink[node] = Math.Min(context.Lowlink[node], wIndex);
            }
        }

        return false;
    }

    /// <summary>
    /// Pops nodes from the Tarjan stack until <paramref name="node"/> is reached,
    /// forming a strongly connected component and adding it to the results.
    /// </summary>
    /// <param name="node">The root node of the strongly connected component.</param>
    /// <param name="context">The context of the Tarjan's algorithm execution.</param>
    private static void CollectSccComponent(Guid node, TarjanContext context)
    {
        var component = new List<Guid>();
        Guid w;
        do
        {
            w = context.Stack.Pop();
            context.OnStack.Remove(w);
            component.Add(w);
        } while (w != node);

        context.Sccs.Add(component);
    }
}
