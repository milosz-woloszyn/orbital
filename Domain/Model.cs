namespace Orbital.Domain;

public readonly record struct Cell(int X, int Y)
{
    public static Cell operator +(Cell a, Cell b) => new(a.X + b.X, a.Y + b.Y);
    public int DistanceTo(Cell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
}

[Flags]
public enum Direction { None = 0, North = 1, East = 2, South = 4, West = 8 }
public enum WireKind { Power, Control, EngineData, PowerData, WeaponData, ShieldData }
public enum ModuleKind
{
    Generator, Battery, Emergency, Feedback, Engine, Dash, Command,
    EngineControl, PowerControl, WeaponControl, ShieldControl, Blaster, Rocket, Shield
}
public enum WeaponMode { Left, Right, Passive, Unassigned }
public enum ModuleState { Idle, Active, NoPower, NoControl, Shed }

public static class Directions
{
    public static readonly IReadOnlyList<Direction> All = Array.AsReadOnly(new[]
        { Direction.North, Direction.East, Direction.South, Direction.West });
    public static Cell Offset(this Direction direction) => direction switch
    {
        Direction.North => new(0, -1), Direction.East => new(1, 0),
        Direction.South => new(0, 1), Direction.West => new(-1, 0), _ => default
    };
    public static Direction Opposite(this Direction direction) => direction switch
    {
        Direction.North => Direction.South, Direction.South => Direction.North,
        Direction.East => Direction.West, Direction.West => Direction.East, _ => Direction.None
    };
    public static Direction Between(Cell from, Cell to) => All.FirstOrDefault(d => from + d.Offset() == to);
    public static Direction Rotate(this Direction direction, int turns)
    {
        for (int i = 0; i < ((turns % 4) + 4) % 4; i++)
            direction = direction switch { Direction.North => Direction.East, Direction.East => Direction.South,
                Direction.South => Direction.West, Direction.West => Direction.North, _ => Direction.None };
        return direction;
    }
    public static bool IsVertical(this Direction direction) => (direction & (Direction.East | Direction.West)) == 0;
}

public sealed record ModuleDefinition(
    ModuleKind Kind, string Name, int Width, int Height,
    double Consumption = 0, double Generation = 0, double Capacity = 0,
    double Speed = 0, double ShotInterval = 0, double ShotEnergy = 0,
    double Damage = 0, double ProjectileSpeed = 0, double BlastRadius = 0,
    double Duration = 0, double Cooldown = 0, double ShieldCapacity = 0,
    double ShieldRecharge = 0, double FeedbackEnergy = 0)
{
    public bool IsWeapon => Kind is ModuleKind.Blaster or ModuleKind.Rocket;
    public bool IsController => Kind is ModuleKind.EngineControl or ModuleKind.PowerControl
        or ModuleKind.WeaponControl or ModuleKind.ShieldControl;
    public bool IsEnergy => Kind is ModuleKind.Generator or ModuleKind.Battery or ModuleKind.Emergency or ModuleKind.Feedback;
    public ModuleKind? RequiredController => Kind switch
    {
        ModuleKind.Battery or ModuleKind.Emergency or ModuleKind.Feedback => ModuleKind.PowerControl,
        ModuleKind.Engine or ModuleKind.Dash => ModuleKind.EngineControl,
        ModuleKind.Blaster or ModuleKind.Rocket => ModuleKind.WeaponControl,
        ModuleKind.Shield => ModuleKind.ShieldControl,
        ModuleKind.EngineControl or ModuleKind.PowerControl or ModuleKind.WeaponControl or ModuleKind.ShieldControl => ModuleKind.Command,
        _ => null
    };
}

public static class ModuleCatalog
{
    public static IReadOnlyList<ModuleDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new ModuleDefinition(ModuleKind.Generator, "Main generator", 2, 2, Generation: 32),
        new ModuleDefinition(ModuleKind.Battery, "Battery", 2, 1, Capacity: 120),
        new ModuleDefinition(ModuleKind.Emergency, "Emergency generator", 2, 2, Generation: 45, Duration: 3, Cooldown: 9),
        new ModuleDefinition(ModuleKind.Feedback, "Feedback generator", 1, 2, FeedbackEnergy: 12),
        new ModuleDefinition(ModuleKind.Engine, "Engine", 2, 2, Consumption: 12, Speed: 240),
        new ModuleDefinition(ModuleKind.Dash, "Dash engine", 2, 1, Consumption: 65, Speed: 850, Duration: .18),
        new ModuleDefinition(ModuleKind.Command, "Command", 2, 2, Consumption: 2),
        new ModuleDefinition(ModuleKind.EngineControl, "Engine controller", 1, 1, Consumption: 2),
        new ModuleDefinition(ModuleKind.PowerControl, "Power controller", 1, 1, Consumption: 2),
        new ModuleDefinition(ModuleKind.WeaponControl, "Weapon controller", 1, 1, Consumption: 2),
        new ModuleDefinition(ModuleKind.ShieldControl, "Shield controller", 1, 1, Consumption: 2),
        new ModuleDefinition(ModuleKind.Blaster, "Blaster", 1, 2, ShotInterval: .14, ShotEnergy: 1.4, Damage: 10, ProjectileSpeed: 700),
        new ModuleDefinition(ModuleKind.Rocket, "Rocket", 2, 1, ShotInterval: .8, ShotEnergy: 7, Damage: 45, ProjectileSpeed: 340, BlastRadius: 85),
        new ModuleDefinition(ModuleKind.Shield, "Shield emitter", 2, 2, Consumption: 16, ShieldCapacity: 70, ShieldRecharge: 20)
    });
    public static ModuleDefinition Get(ModuleKind kind) => All[(int)kind];
}

public sealed class ShipModule
{
    internal ShipModule(int id, ModuleKind kind, Cell position, int rotation)
    { Id = id; Kind = kind; Position = position; Rotation = rotation; }
    public int Id { get; }
    public ModuleKind Kind { get; }
    public ModuleDefinition Definition => ModuleCatalog.Get(Kind);
    public Cell Position { get; internal set; }
    public int Rotation { get; internal set; }
    public int Level { get; internal set; } = 1;
    private int priority = 5;
    public int Priority { get => priority; set => priority = Math.Clamp(value, 1, 10); }
    public WeaponMode WeaponMode { get; internal set; } = WeaponMode.Unassigned;
    public double BurstDuration { get; internal set; }
    public double BurstGap { get; internal set; }
    public int Width => Rotation % 2 == 0 ? Definition.Width : Definition.Height;
    public int Height => Rotation % 2 == 0 ? Definition.Height : Definition.Width;
    public double UpgradeFactor => 1 + (Level - 1) * .2;
    public IEnumerable<Cell> Cells
    {
        get { for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) yield return Position + new Cell(x, y); }
    }
    public WireKind? DataInputKind => Kind switch
    {
        // The main generator is autonomous and exposes only its power terminal.
        ModuleKind.Generator => null,
        ModuleKind.EngineControl or ModuleKind.PowerControl or ModuleKind.WeaponControl or ModuleKind.ShieldControl => WireKind.Control,
        ModuleKind.Engine or ModuleKind.Dash => WireKind.EngineData,
        ModuleKind.Battery or ModuleKind.Emergency or ModuleKind.Feedback => WireKind.PowerData,
        ModuleKind.Blaster or ModuleKind.Rocket => WireKind.WeaponData,
        ModuleKind.Shield => WireKind.ShieldData,
        _ => null
    };
    public WireKind? DataOutputKind => Kind switch
    {
        ModuleKind.Command => WireKind.Control,
        ModuleKind.EngineControl => WireKind.EngineData,
        ModuleKind.PowerControl => WireKind.PowerData,
        ModuleKind.WeaponControl => WireKind.WeaponData,
        ModuleKind.ShieldControl => WireKind.ShieldData,
        _ => null
    };
    public bool HasPort(WireKind kind) => kind == WireKind.Power || kind == DataInputKind || kind == DataOutputKind;

    // Local terminals rotate with the footprint. Controller input and output are separate leaves.
    public ModulePort Port(WireKind kind)
    {
        if (!HasPort(kind)) throw new ArgumentException("Module does not support this wire kind.", nameof(kind));
        bool commandInput = Definition.IsController && kind == WireKind.Control;
        int x = kind == WireKind.Power || commandInput ? 0 : Definition.Width - 1;
        int y = kind == WireKind.Power || commandInput ? 0 : Definition.Height - 1;
        int width = Definition.Width, height = Definition.Height;
        for (int i = 0; i < Rotation; i++) { (x, y) = (height - 1 - y, x); (width, height) = (height, width); }
        var facing = (kind == WireKind.Power ? Direction.North : commandInput ? Direction.West : Direction.South).Rotate(Rotation);
        return new(Position + new Cell(x, y), facing);
    }
}

public readonly record struct ModulePort(Cell Cell, Direction Facing)
{
    public Cell CableCell => Cell + Facing.Offset();
}
public sealed record WireTile(Cell Cell, WireKind Kind, Direction Connections);
public readonly record struct EditResult(bool Success, string Error = "")
{
    public static EditResult Ok => new(true);
    public static EditResult Fail(string error) => new(false, error);
}

public sealed record ModuleStatus(int ModuleId, ModuleState State, string Reason,
    int PowerIsland, int SourceDistance, double Demand, double StoredEnergy, double Shield,
    double CooldownRemaining = 0)
{
    public bool Active => State == ModuleState.Active;
    public bool Powered => State is ModuleState.Idle or ModuleState.Active;
}

/// <summary>Movement and fire are held intentions; dash is a press edge. WeaponRequests requests specific
/// module IDs in addition to mouse bindings; requests still obey the module's normal shot interval.</summary>
public sealed record FrameIntent(bool MoveRequested = false, bool DashRequested = false,
    bool LeftFire = false, bool RightFire = false, IReadOnlyCollection<int>? WeaponRequests = null);
public sealed record WeaponShot(int ModuleId, ModuleKind Kind, double Damage, double Speed, double BlastRadius,
    double TimeOffset);
public sealed record SimulationFrame(IReadOnlyList<WeaponShot> Shots, double MoveSpeed, double DashSpeed,
    bool DashActive, bool DashStarted, bool CanSteer);
public sealed record DamageResult(double Absorbed, double HullDamage, double FeedbackEnergy);
