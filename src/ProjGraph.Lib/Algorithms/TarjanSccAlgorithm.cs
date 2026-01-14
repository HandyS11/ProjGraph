using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Algorithms;

public static class TarjanSccAlgorithm
{
    private sealed class TarjanContext
    {
        public int Index { get; set; }
        public Stack<Guid> Stack { get; } = new();
        public Dictionary<Guid, int> Indices { get; } = [];
        public Dictionary<Guid, int> Lowlink { get; } = [];
        public HashSet<Guid> OnStack { get; } = [];
        public List<List<Guid>> Sccs { get; } = [];
    }

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
