using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Algorithms;

public interface IDependencyGraph
{
    IReadOnlyList<IReadOnlyList<Guid>> FindStronglyConnectedComponents(SolutionGraph graph);
}

public class TarjanSccAlgorithm : IDependencyGraph
{
    public IReadOnlyList<IReadOnlyList<Guid>> FindStronglyConnectedComponents(SolutionGraph graph)
    {
        var adjacencyList = graph.Projects.ToDictionary(
            p => p.Id,
            p => graph.Dependencies.Where(d => d.SourceId == p.Id).Select(d => d.TargetId).ToList()
        );

        var index = 0;
        var stack = new Stack<Guid>();
        var indices = new Dictionary<Guid, int>();
        var lowlink = new Dictionary<Guid, int>();
        var onStack = new HashSet<Guid>();
        var sccs = new List<List<Guid>>();

        foreach (var v in graph.Projects.Select(p => p.Id))
        {
            if (!indices.ContainsKey(v))
            {
                StrongConnect(v, adjacencyList, ref index, stack, indices, lowlink, onStack, sccs);
            }
        }

        return sccs;
    }

    private void StrongConnect(
        Guid v,
        Dictionary<Guid, List<Guid>> adjacencyList,
        ref int index,
        Stack<Guid> stack,
        Dictionary<Guid, int> indices,
        Dictionary<Guid, int> lowlink,
        HashSet<Guid> onStack,
        List<List<Guid>> sccs)
    {
        indices[v] = index;
        lowlink[v] = index;
        index++;
        stack.Push(v);
        onStack.Add(v);

        if (adjacencyList.TryGetValue(v, out var neighbors))
        {
            foreach (var w in neighbors)
            {
                if (!indices.TryGetValue(w, out var index1))
                {
                    StrongConnect(w, adjacencyList, ref index, stack, indices, lowlink, onStack, sccs);
                    lowlink[v] = Math.Min(lowlink[v], lowlink[w]);
                }
                else if (onStack.Contains(w))
                {
                    lowlink[v] = Math.Min(lowlink[v], index1);
                }
            }
        }

        if (lowlink[v] == indices[v])
        {
            var component = new List<Guid>();
            Guid w;
            do
            {
                w = stack.Pop();
                onStack.Remove(w);
                component.Add(w);
            } while (w != v);

            sccs.Add(component);
        }
    }
}
