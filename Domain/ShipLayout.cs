namespace Orbital.Domain;

public sealed class ShipLayout
{
    private readonly List<ShipModule> modules = new();
    private readonly Dictionary<(Cell, WireKind), WireTile> wires = new();
    private readonly HashSet<Cell> bridges = new();
    private int nextId = 1;
    public ShipLayout(int width = 16, int height = 10)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height;
    }
    public int Width { get; }
    public int Height { get; }
    public int Revision { get; private set; }
    public IReadOnlyList<ShipModule> Modules => modules.AsReadOnly();
    public IReadOnlyCollection<WireTile> Wires => wires.Values;
    public IReadOnlyCollection<Cell> Bridges => bridges.ToArray();
    public bool Contains(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
    public ShipModule? ModuleAt(Cell cell) => modules.FirstOrDefault(m => m.Cells.Contains(cell));
    public ShipModule? Find(int id) => modules.FirstOrDefault(m => m.Id == id);
    public WireTile? WireAt(Cell cell, WireKind kind) => wires.GetValueOrDefault((cell, kind));
    public bool IsBridge(Cell cell) => bridges.Contains(cell);

    public EditResult CanPlace(ModuleKind kind, Cell position, int rotation = 0, int? ignoreId = null)
    {
        if (!Enum.IsDefined(kind)) return EditResult.Fail("Nieznany rodzaj modułu.");
        var candidate = new ShipModule(0, kind, position, NormalizeRotation(rotation));
        foreach (var cell in candidate.Cells)
        {
            if (!Contains(cell)) return EditResult.Fail("Moduł wychodzi poza siatkę.");
            if (ModuleAt(cell) is { } existing && existing.Id != ignoreId) return EditResult.Fail("To pole zajmuje inny moduł.");
            if (wires.Keys.Any(k => k.Item1 == cell) || bridges.Contains(cell)) return EditResult.Fail("Najpierw usuń kable i mostki z tych pól.");
        }
        return EditResult.Ok;
    }
    public EditResult TryAdd(ModuleKind kind, Cell position, out ShipModule? module, int rotation = 0)
    {
        module = null;
        var result = CanPlace(kind, position, rotation);
        if (!result.Success) return result;
        module = new ShipModule(nextId++, kind, position, NormalizeRotation(rotation));
        modules.Add(module); Revision++;
        return EditResult.Ok;
    }
    public EditResult TryMove(int id, Cell position, int? rotation = null)
    {
        var module = Find(id);
        if (module is null) return EditResult.Fail("Moduł nie istnieje.");
        int turns = NormalizeRotation(rotation ?? module.Rotation);
        var result = CanPlace(module.Kind, position, turns, id);
        if (!result.Success) return result;
        module.Position = position; module.Rotation = turns; Revision++;
        return EditResult.Ok;
    }
    public EditResult TryRotate(int id) => Find(id) is { } module
        ? TryMove(id, module.Position, module.Rotation + 1) : EditResult.Fail("Moduł nie istnieje.");
    public bool Remove(int id)
    {
        if (modules.RemoveAll(m => m.Id == id) == 0) return false;
        Revision++; return true;
    }
    public bool Upgrade(int id)
    {
        if (Find(id) is not { } module || module.Level == int.MaxValue) return false;
        module.Level++; Revision++; return true;
    }
    public EditResult ConfigureWeapon(int id, WeaponMode mode, double burstDuration = 0, double burstGap = 0)
    {
        if (Find(id) is not { Definition.IsWeapon: true } module) return EditResult.Fail("Wybierz moduł uzbrojenia.");
        if (!Enum.IsDefined(mode) || !double.IsFinite(burstDuration) || !double.IsFinite(burstGap) || burstDuration < 0 || burstGap < 0)
            return EditResult.Fail("Czas serii i przerwy musi być skończoną, nieujemną liczbą sekund.");
        if (mode is WeaponMode.Left or WeaponMode.Right)
            foreach (var other in modules.Where(m => m.Id != id && m.WeaponMode == mode)) other.WeaponMode = WeaponMode.Unassigned;
        module.WeaponMode = mode; module.BurstDuration = burstDuration; module.BurstGap = burstGap;
        Revision++; return EditResult.Ok;
    }

    public EditResult TrySetBridge(Cell cell, bool enabled)
    {
        if (!Contains(cell) || ModuleAt(cell) is not null) return EditResult.Fail("Mostek wymaga pola bez modułu.");
        if (!enabled && WireAt(cell, WireKind.Power) is not null && WireAt(cell, WireKind.Control) is not null)
            return EditResult.Fail("Przed usunięciem mostka usuń jeden z krzyżujących się kabli.");
        if (enabled) bridges.Add(cell); else bridges.Remove(cell);
        Revision++; return EditResult.Ok;
    }
    public bool RemoveWire(Cell cell, WireKind kind)
    {
        if (!wires.Remove((cell, kind))) return false;
        // Remove the arms that used to enter this tile; stubs against module ports remain valid.
        foreach (var direction in Directions.All)
        {
            var key = (cell + direction.Offset(), kind);
            if (wires.TryGetValue(key, out var neighbor)) wires[key] = neighbor with { Connections = neighbor.Connections & ~direction.Opposite() };
        }
        Revision++; return true;
    }
    public void ClearWires() { wires.Clear(); bridges.Clear(); Revision++; }

    /// <summary>Draws a contiguous orthogonal path atomically. Ends beside ports attach automatically.</summary>
    public EditResult TryDrawWire(WireKind kind, IReadOnlyList<Cell> path, bool autoBridge = false)
    {
        if (path.Count == 0) return EditResult.Fail("Trasa wymaga co najmniej jednego pola.");
        if (!Enum.IsDefined(kind)) return EditResult.Fail("Nieznany rodzaj kabla.");
        var additions = new Dictionary<Cell, Direction>();
        for (int i = 0; i < path.Count; i++)
        {
            var cell = path[i];
            if (!Contains(cell) || ModuleAt(cell) is not null) return EditResult.Fail("Kable muszą mieścić się w siatce i nie mogą przebiegać pod modułami.");
            Direction arms = additions.GetValueOrDefault(cell) | (WireAt(cell, kind)?.Connections ?? Direction.None);
            if (i > 0)
            {
                var direction = Directions.Between(cell, path[i - 1]);
                if (direction == Direction.None) return EditResult.Fail("Kolejne pola kabla muszą stykać się bokami.");
                arms |= direction;
            }
            if (i + 1 < path.Count)
            {
                var direction = Directions.Between(cell, path[i + 1]);
                if (direction == Direction.None) return EditResult.Fail("Kolejne pola kabla muszą stykać się bokami.");
                arms |= direction;
            }
            foreach (var module in modules)
            {
                var port = module.Port(kind);
                if (port.CableCell == cell) arms |= port.Facing.Opposite();
            }
            additions[cell] = arms;
        }
        var newBridges = new HashSet<Cell>();
        foreach (var (cell, arms) in additions)
        {
            var other = WireAt(cell, kind == WireKind.Power ? WireKind.Control : WireKind.Power);
            if (other is null) continue;
            if (!(IsBridge(cell) || autoBridge)) return EditResult.Fail("Skrzyżowanie różnych rodzajów kabli wymaga mostka.");
            if (!OneAxis(arms) || !OneAxis(other.Connections) || arms.IsVertical() == other.Connections.IsVertical())
                return EditResult.Fail("Na mostku różne kable muszą biec prosto w osobnych osiach północ–południe i wschód–zachód.");
            newBridges.Add(cell);
        }
        // A bridge may have both axes of one cable type, but each traversal must be straight.
        for (int i = 1; i + 1 < path.Count; i++)
            if ((IsBridge(path[i]) || newBridges.Contains(path[i])) &&
                Directions.Between(path[i], path[i - 1]).Opposite() != Directions.Between(path[i], path[i + 1]))
                return EditResult.Fail("Kabel nie może skręcać na mostku.");
        foreach (var (cell, arms) in additions) wires[(cell, kind)] = new(cell, kind, arms);
        bridges.UnionWith(newBridges); Revision++; return EditResult.Ok;
    }

    public IReadOnlyList<Cell>? FindRoute(Cell start, Cell end, WireKind kind, bool autoBridge = true,
        Direction startArm = Direction.None, Direction endArm = Direction.None)
    {
        if (!Contains(start) || !Contains(end) || ModuleAt(start) is not null || ModuleAt(end) is not null) return null;
        // Arrival direction is part of the search state so bridges cannot create hidden turns.
        var initial = (Cell: start, Arrival: startArm);
        var queue = new Queue<(Cell Cell, Direction Arrival)>(); queue.Enqueue(initial);
        var previous = new Dictionary<(Cell, Direction), (Cell, Direction)?> { [initial] = null };
        while (queue.TryDequeue(out var current))
        {
            if (current.Cell == end && CanTraverse(current.Cell, current.Arrival, endArm, kind, autoBridge))
            {
                var result = new List<Cell>();
                (Cell, Direction)? step = current;
                while (step is { } value) { result.Add(value.Item1); step = previous[value]; }
                result.Reverse(); return result;
            }
            foreach (var direction in Directions.All)
            {
                var next = current.Cell + direction.Offset();
                if (!Contains(next) || ModuleAt(next) is not null || !CanTraverse(current.Cell, current.Arrival, direction, kind, autoBridge)) continue;
                var state = (next, direction.Opposite());
                if (previous.ContainsKey(state)) continue;
                previous[state] = current; queue.Enqueue(state);
            }
        }
        return null;
    }
    private bool CanTraverse(Cell cell, Direction incoming, Direction outgoing, WireKind kind, bool autoBridge)
    {
        var other = WireAt(cell, kind == WireKind.Power ? WireKind.Control : WireKind.Power);
        if ((IsBridge(cell) || other is not null) && incoming != Direction.None && outgoing != Direction.None && incoming.Opposite() != outgoing) return false;
        if (other is null) return true;
        var arms = incoming | outgoing | (WireAt(cell, kind)?.Connections ?? Direction.None);
        return (IsBridge(cell) || autoBridge) && OneAxis(arms) && OneAxis(other.Connections) && arms.IsVertical() != other.Connections.IsVertical();
    }
    public EditResult TryConnect(int firstId, int secondId, WireKind kind, bool autoBridge = true)
    {
        var first = Find(firstId); var second = Find(secondId);
        if (first is null || second is null) return EditResult.Fail("Oba moduły muszą istnieć.");
        var a = first.Port(kind); var b = second.Port(kind);
        var route = FindRoute(a.CableCell, b.CableCell, kind, autoBridge, a.Facing.Opposite(), b.Facing.Opposite());
        return route is null ? EditResult.Fail("Brak poprawnej trasy między złączami.") : TryDrawWire(kind, route, autoBridge);
    }
    /// <summary>Preserves existing wires. Returns failures for ports that could not be routed.</summary>
    public IReadOnlyList<string> AutoConnectAll()
    {
        var errors = new List<string>();
        foreach (var kind in Enum.GetValues<WireKind>())
        {
            var candidates = modules.Where(m => kind == WireKind.Power || m.Kind != ModuleKind.Generator).ToArray();
            if (candidates.Length < 2) continue;
            var connected = new List<ShipModule> { candidates[0] };
            foreach (var module in candidates.Skip(1))
            {
                bool success = false;
                foreach (var target in connected.OrderBy(m => m.Port(kind).CableCell.DistanceTo(module.Port(kind).CableCell)))
                    if (TryConnect(target.Id, module.Id, kind).Success) { success = true; break; }
                if (success) connected.Add(module);
                else errors.Add($"{kind}: brak trasy do {module.Definition.Name} #{module.Id}.");
            }
        }
        return errors;
    }
    public WireNetwork BuildNetwork(WireKind kind) => new(this, kind);
    private static int NormalizeRotation(int rotation) => ((rotation % 4) + 4) % 4;
    private static bool OneAxis(Direction arms) => arms != Direction.None &&
        ((arms & (Direction.North | Direction.South)) == 0 || (arms & (Direction.East | Direction.West)) == 0);

    public static ShipLayout CreateStarter()
    {
        var layout = new ShipLayout();
        var kinds = new[] { ModuleKind.Generator, ModuleKind.Battery, ModuleKind.Engine, ModuleKind.Command,
            ModuleKind.Blaster, ModuleKind.PowerControl, ModuleKind.EngineControl, ModuleKind.WeaponControl, ModuleKind.Rocket };
        for (int i = 0; i < kinds.Length; i++)
        {
            var position = i < 5 ? new Cell(1 + 3 * i, 1) : new Cell(1 + 3 * (i - 5), 5);
            layout.TryAdd(kinds[i], position, out var module);
            module!.Priority = module.Kind is ModuleKind.Command or ModuleKind.PowerControl ? 10 : module.Definition.IsController ? 9 : 5;
            if (module.Kind == ModuleKind.Blaster) layout.ConfigureWeapon(module.Id, WeaponMode.Left);
            if (module.Kind == ModuleKind.Rocket) layout.ConfigureWeapon(module.Id, WeaponMode.Right);
        }
        // Separate horizontal buses joined at opposite edges leave room for later edits.
        Draw(WireKind.Power, Line(new(0, 0), new(13, 0)));
        Draw(WireKind.Power, Line(new(0, 0), new(0, 4)));
        Draw(WireKind.Power, Line(new(0, 4), new(10, 4)));
        Draw(WireKind.Control, Line(new(0, 3), new(15, 3)));
        Draw(WireKind.Control, Line(new(15, 3), new(15, 7)));
        Draw(WireKind.Control, Line(new(1, 7), new(15, 7)));
        foreach (var module in layout.Modules.Where(m => m.Kind != ModuleKind.Generator))
        {
            var port = module.Port(WireKind.Control);
            Draw(WireKind.Control, Line(port.CableCell, new(port.CableCell.X, module.Position.Y < 4 ? 3 : 7)));
        }
        return layout;
        void Draw(WireKind kind, IReadOnlyList<Cell> path)
        {
            var result = layout.TryDrawWire(kind, path, true);
            if (!result.Success) throw new InvalidOperationException(result.Error);
        }
    }
    private static IReadOnlyList<Cell> Line(Cell from, Cell to)
    {
        var result = new List<Cell> { from };
        while (from != to) { from = new(from.X + Math.Sign(to.X - from.X), from.Y + Math.Sign(to.Y - from.Y)); result.Add(from); }
        return result;
    }
}
