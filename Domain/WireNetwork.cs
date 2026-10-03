namespace Orbital.Domain;

/// <summary>A cable graph. Module ports are leaves, never paths between cable networks.</summary>
public sealed class WireNetwork
{
    private readonly record struct Node(Cell Cell, int Lane);
    private readonly Dictionary<Node, List<Node>> edges = new();
    private readonly Dictionary<Node, int> components = new();
    private readonly Dictionary<int, Node> ports = new();
    private readonly Dictionary<int, int> islands = new();
    public WireKind Kind { get; }
    public IReadOnlyDictionary<int, int> ModuleIslands => islands;
    internal WireNetwork(ShipLayout layout, WireKind kind)
    {
        Kind = kind;
        Node NodeFor(Cell cell, Direction direction) => new(cell, layout.IsBridge(cell) ? (direction.IsVertical() ? 1 : 2) : 0);
        foreach (var tile in layout.Wires.Where(w => w.Kind == kind))
        {
            foreach (var direction in Directions.All.Where(d => tile.Connections.HasFlag(d)))
            {
                var node = NodeFor(tile.Cell, direction);
                if (!edges.ContainsKey(node)) edges[node] = new();
                var nextCell = tile.Cell + direction.Offset();
                if (layout.WireAt(nextCell, kind) is { } next && next.Connections.HasFlag(direction.Opposite()))
                    edges[node].Add(NodeFor(nextCell, direction.Opposite()));
            }
        }
        int component = 0;
        foreach (var start in edges.Keys)
        {
            if (components.ContainsKey(start)) continue;
            var queue = new Queue<Node>(); queue.Enqueue(start); components[start] = component;
            while (queue.TryDequeue(out var current))
                foreach (var next in edges[current]) if (components.TryAdd(next, component)) queue.Enqueue(next);
            component++;
        }
        foreach (var module in layout.Modules.Where(m => m.HasPort(kind)))
        {
            var port = module.Port(kind);
            var node = NodeFor(port.CableCell, port.Facing.Opposite());
            if (layout.WireAt(port.CableCell, kind) is { } tile && tile.Connections.HasFlag(port.Facing.Opposite()) && components.TryGetValue(node, out int island))
            { ports[module.Id] = node; islands[module.Id] = island; }
            else islands[module.Id] = component++; // Unwired terminals do not share an island.
        }
    }
    public int IslandOf(int moduleId) => islands.GetValueOrDefault(moduleId, -1);
    public bool Connected(int firstId, int secondId) => firstId == secondId ? islands.ContainsKey(firstId) :
        ports.ContainsKey(firstId) && ports.ContainsKey(secondId) && IslandOf(firstId) == IslandOf(secondId);
    public int Distance(int firstId, int secondId)
    {
        if (firstId == secondId) return islands.ContainsKey(firstId) ? 0 : int.MaxValue;
        if (!Connected(firstId, secondId)) return int.MaxValue;
        var start = ports[firstId]; var target = ports[secondId];
        var distances = new Dictionary<Node, int> { [start] = 1 };
        var queue = new Queue<Node>(); queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
        {
            if (current == target) return distances[current];
            foreach (var next in edges[current])
                if (distances.TryAdd(next, distances[current] + 1)) queue.Enqueue(next);
        }
        return int.MaxValue;
    }
    public int DistanceToNearest(int moduleId, IEnumerable<int> sourceIds) => sourceIds
        .Select(id => Distance(moduleId, id)).DefaultIfEmpty(int.MaxValue).Min();
}
