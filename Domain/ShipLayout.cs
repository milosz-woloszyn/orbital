namespace Orbital.Domain;

public sealed class ShipLayout
{
    private readonly List<ShipModule> modules = new();
    private readonly Dictionary<(Cell, WireKind), WireTile> wires = new();
    private readonly HashSet<Cell> bridges = new();
    private int nextId = 1;
    public ShipLayout(int width = 18, int height = 12)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height;
    }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Revision { get; private set; }
    public IReadOnlyList<ShipModule> Modules => modules.AsReadOnly();
    public IReadOnlyCollection<WireTile> Wires => wires.Values;
    public IReadOnlyCollection<Cell> Bridges => bridges.ToArray();
    public void Grow(int columns, int rows)
    {
        if(columns<0 || rows<0)throw new ArgumentOutOfRangeException(nameof(columns));
        Width=checked(Width+columns);Height=checked(Height+rows);Revision++;
    }
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
        return ValidateWires(wires, bridges, modules.Where(m => m.Id != ignoreId).Append(candidate));
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
        if (!enabled && wires.Values.Count(w => w.Cell == cell) > 1)
            return EditResult.Fail("Przed usunięciem mostka usuń jeden z krzyżujących się kabli.");
        var proposed = new HashSet<Cell>(bridges);
        if (enabled) proposed.Add(cell); else proposed.Remove(cell);
        var validation = ValidateWires(wires, proposed, modules);
        if (!validation.Success) return validation;
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
            foreach (var module in modules.Where(m => m.HasPort(kind)))
            {
                var port = module.Port(kind);
                if (port.CableCell == cell) arms |= port.Facing.Opposite();
            }
            if (i == 0 || i == path.Count - 1)
                foreach (var module in modules)
                    foreach (var terminalKind in Enum.GetValues<WireKind>().Where(module.HasPort))
                    {
                        if (terminalKind == kind) continue;
                        var terminal = module.Port(terminalKind);
                        if (terminal.CableCell == cell &&
                            !(IsBridge(cell) && OneAxis(arms) && arms.IsVertical() != terminal.Facing.IsVertical()))
                            return EditResult.Fail("Koniec kabla nie pasuje do rodzaju złącza modułu.");
                    }
            additions[cell] = arms;
        }
        var newBridges = new HashSet<Cell>();
        foreach (var cell in additions.Keys)
            if (wires.Values.Any(w => w.Cell == cell && w.Kind != kind) && autoBridge) newBridges.Add(cell);
        var proposedWires = new Dictionary<(Cell, WireKind), WireTile>(wires);
        foreach (var (cell, arms) in additions) proposedWires[(cell, kind)] = new(cell, kind, arms);
        var proposedBridges = new HashSet<Cell>(bridges);
        proposedBridges.UnionWith(newBridges);
        var validation = ValidateWires(proposedWires, proposedBridges, modules);
        if (!validation.Success) return validation;
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
        if (!Enum.IsDefined(kind) || !Contains(start) || !Contains(end) || ModuleAt(start) is not null || ModuleAt(end) is not null) return null;
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
        var others = wires.Values.Where(w => w.Cell == cell && w.Kind != kind).ToArray();
        bool bridge = IsBridge(cell) || autoBridge && others.Length > 0;
        if (bridge && incoming != Direction.None && outgoing != Direction.None && incoming.Opposite() != outgoing) return false;
        var arms = incoming | outgoing | (WireAt(cell, kind)?.Connections ?? Direction.None);
        foreach (var module in modules.Where(m => m.HasPort(kind)))
        {
            var port = module.Port(kind);
            if (port.CableCell == cell) arms |= port.Facing.Opposite();
        }
        if (others.Any(other => !bridge || !SeparateAxes(arms, other.Connections))) return false;
        if (!CompatibleTerminals(new(cell, kind, arms), bridge, modules)) return false;
        return !Directions.All.Any(d => arms.HasFlag(d) && wires.Values.Any(w =>
            w.Cell == cell + d.Offset() && w.Kind != kind && w.Connections.HasFlag(d.Opposite())));
    }

    // Validate proposed topology before committing any edit, including revision and module IDs.
    private static EditResult ValidateWires(Dictionary<(Cell, WireKind), WireTile> proposed,
        HashSet<Cell> proposedBridges, IEnumerable<ShipModule> proposedModules)
    {
        var terminals = proposedModules.ToArray();
        foreach (var tile in proposed.Values)
        {
            bool bridge = proposedBridges.Contains(tile.Cell);
            foreach (var other in proposed.Values.Where(w => w.Cell == tile.Cell && w.Kind != tile.Kind))
                if (!bridge || !SeparateAxes(tile.Connections, other.Connections))
                    return EditResult.Fail("Różne sieci wymagają mostka i osobnych prostych osi.");
            if (!CompatibleTerminals(tile, bridge, terminals))
                return EditResult.Fail("Kabel nie pasuje do rodzaju złącza modułu.");
            foreach (var direction in Directions.All.Where(d => tile.Connections.HasFlag(d)))
                foreach (var kind in Enum.GetValues<WireKind>().Where(k => k != tile.Kind))
                    if (proposed.TryGetValue((tile.Cell + direction.Offset(), kind), out var neighbor) &&
                        neighbor.Connections.HasFlag(direction.Opposite()))
                        return EditResult.Fail("Nie można łączyć kabli różnych sieci.");
        }
        return EditResult.Ok;
    }
    private static bool CompatibleTerminals(WireTile tile, bool bridge, IEnumerable<ShipModule> candidates)
    {
        foreach (var module in candidates)
            foreach (var kind in Enum.GetValues<WireKind>().Where(module.HasPort))
            {
                if (kind == tile.Kind) continue;
                var port = module.Port(kind);
                if (port.CableCell != tile.Cell) continue;
                var arm = port.Facing.Opposite();
                if (tile.Connections.HasFlag(arm)) return false;
            }
        return true;
    }
    private static bool SeparateAxes(Direction first, Direction second) =>
        OneAxis(first) && OneAxis(second) && first.IsVertical() != second.IsVertical();

    public EditResult TryConnect(int firstId, int secondId, WireKind kind, bool autoBridge = true)
    {
        var first = Find(firstId); var second = Find(secondId);
        if (first is null || second is null) return EditResult.Fail("Oba moduły muszą istnieć.");
        if (!first.HasPort(kind) || !second.HasPort(kind)) return EditResult.Fail("Moduły nie mają złączy dla tej sieci.");
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
            var candidates = modules.Where(m => m.HasPort(kind)).ToArray();
            if (candidates.Length < 2) continue;
            var connected = new List<ShipModule> { candidates[0] };
            foreach (var module in candidates.Skip(1))
            {
                bool success = connected.Any(target => BuildNetwork(kind).Connected(target.Id, module.Id));
                foreach (var target in connected.Where(_ => !success).OrderBy(m => m.Port(kind).CableCell.DistanceTo(module.Port(kind).CableCell)))
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
        var placements = new (ModuleKind Kind, Cell Position)[]
        {
            (ModuleKind.Generator, new(1, 1)), (ModuleKind.Battery, new(4, 1)),
            (ModuleKind.Engine, new(9, 1)), (ModuleKind.Blaster, new(13, 1)),
            (ModuleKind.Rocket, new(15, 1)), (ModuleKind.Command, new(1, 6)),
            (ModuleKind.PowerControl, new(5, 6)), (ModuleKind.EngineControl, new(9, 6)),
            (ModuleKind.WeaponControl, new(13, 6))
        };
        foreach (var (kind, position) in placements)
        {
            var result = layout.TryAdd(kind, position, out var module);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            module!.Priority = module.Kind is ModuleKind.Command or ModuleKind.PowerControl ? 10 : module.Definition.IsController ? 9 : 5;
            if (module.Kind == ModuleKind.Blaster) layout.ConfigureWeapon(module.Id, WeaponMode.Left);
            if (module.Kind == ModuleKind.Rocket) layout.ConfigureWeapon(module.Id, WeaponMode.Right);
        }
        Draw(WireKind.Power, Line(new(0, 0), new(15, 0)));
        Draw(WireKind.Power, Line(new(0, 0), new(0, 5)));
        Draw(WireKind.Power, Line(new(0, 5), new(13, 5)));
        Draw(WireKind.Control, Line(new(2, 8), new(12, 8)));
        foreach (int x in new[] { 4, 8, 12 }) Draw(WireKind.Control, Line(new(x, 8), new(x, 6)));
        Draw(WireKind.PowerData, Line(new(5, 3), new(6, 3)));
        Draw(WireKind.PowerData, Line(new(5, 2), new(5, 3)));
        Draw(WireKind.PowerData, Line(new(6, 3), new(6, 7)));
        Draw(WireKind.PowerData, Line(new(5, 7), new(6, 7)));
        Draw(WireKind.EngineData, Line(new(10, 3), new(10, 7)));
        Draw(WireKind.EngineData, Line(new(9, 7), new(10, 7)));
        Draw(WireKind.WeaponData, Line(new(13, 3), new(16, 3)));
        Draw(WireKind.WeaponData, Line(new(16, 2), new(16, 3)));
        Draw(WireKind.WeaponData, Line(new(14, 3), new(14, 7)));
        Draw(WireKind.WeaponData, Line(new(13, 7), new(14, 7)));
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
