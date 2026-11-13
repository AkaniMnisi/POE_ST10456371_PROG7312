using SmartX.Models;
namespace SmartX.Algorithms;

public sealed class MeshGraph
{
    private readonly Dictionary<string, List<(string To, double Cost)>> adjacency = new(StringComparer.OrdinalIgnoreCase);
    public MeshGraph(IEnumerable<Device> devices, IEnumerable<Link> links)
    {
        foreach (var d in devices.Where(d => d.Online)) adjacency[d.Id] = [];
        foreach (var e in links)
        {
            if (!adjacency.ContainsKey(e.From) || !adjacency.ContainsKey(e.To)) continue;
            adjacency[e.From].Add((e.To, e.Weight)); adjacency[e.To].Add((e.From, e.Weight));
        }
    }
    // (Sedgewick and Wayne, 2011b): BFS discovers the connected component in O(V+E).
    public string[] BreadthFirst(string start)
    {
        if (!adjacency.ContainsKey(start)) return [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { start };
        Queue<string> queue = new(); List<string> order = []; queue.Enqueue(start);
        while (queue.TryDequeue(out var node))
        {
            order.Add(node);
            foreach (var (to, _) in adjacency[node]) if (seen.Add(to)) queue.Enqueue(to);
        }
        return order.ToArray();
    }
    // Lazy Prim (Sedgewick and Wayne, 2011c). Restart each component to produce a forest.
    // Heap-backed PriorityQueue (Microsoft, n.d.a) selects the cheapest frontier link.
    public (Link[] Edges, int Components) MinimumSpanningForest()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase); List<Link> edges = []; int components = 0;
        foreach (var start in adjacency.Keys.Order())
        {
            if (seen.Contains(start)) continue;
            components++; PriorityQueue<Link, double> heap = new(); Visit(start);
            while (heap.TryDequeue(out var edge, out _))
            {
                if (seen.Contains(edge.To)) continue;
                edges.Add(edge); Visit(edge.To);
            }
            void Visit(string node)
            {
                seen.Add(node);
                foreach (var (to, cost) in adjacency[node]) if (!seen.Contains(to)) heap.Enqueue(new(node, to, cost), cost);
            }
        }
        return (edges.ToArray(), components);
    }
    // Dijkstra (Sedgewick and Wayne, 2011d) finds a minimum-cost individual route.
    // An MST minimises TOTAL network cost; it does not guarantee minimum per-device path cost.
    public (string[] Path, double? Cost) ShortestPath(string start, string target)
    {
        if (!adjacency.ContainsKey(start) || !adjacency.ContainsKey(target)) return ([], null);
        Dictionary<string, double> dist = new(StringComparer.OrdinalIgnoreCase) { [start] = 0 };
        Dictionary<string, string> previous = new(StringComparer.OrdinalIgnoreCase);
        PriorityQueue<string, double> heap = new(); heap.Enqueue(start, 0);
        while (heap.TryDequeue(out var node, out var cost))
        {
            if (cost > dist[node]) continue;
            if (node.Equals(target, StringComparison.OrdinalIgnoreCase)) break;
            foreach (var (to, weight) in adjacency[node])
            {
                double candidate = cost + weight;
                if (!dist.TryGetValue(to, out var known) || candidate < known)
                { dist[to] = candidate; previous[to] = node; heap.Enqueue(to, candidate); }
            }
        }
        if (!dist.TryGetValue(target, out var total)) return ([], null);
        List<string> path = [target]; var current = target;
        while (!current.Equals(start, StringComparison.OrdinalIgnoreCase)) { current = previous[current]; path.Add(current); }
        path.Reverse(); return (path.ToArray(), total);
    }
}
/* Reference list:
Microsoft (n.d.a) PriorityQueue<TElement,TPriority> Class. https://learn.microsoft.com/dotnet/api/system.collections.generic.priorityqueue-2
Sedgewick, R. and Wayne, K. (2011b) Undirected graphs. https://algs4.cs.princeton.edu/41graph/
Sedgewick, R. and Wayne, K. (2011c) Minimum spanning trees. https://algs4.cs.princeton.edu/43mst/
Sedgewick, R. and Wayne, K. (2011d) Shortest paths. https://algs4.cs.princeton.edu/44sp/
(All accessed: 9 November 2026.)
*/
