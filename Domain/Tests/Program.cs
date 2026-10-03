using Orbital.Domain;

var tests = new (string, Action)[]
{
    ("Starter connectivity and silent refresh", Starter),
    ("Editing is atomic, rotations move ports", Editing),
    ("Crossings merge and bridges split lanes", Bridges),
    ("Independent generator and control dependencies", Dependencies),
    ("Battery before shedding, buffers survive disconnection", Batteries),
    ("Emergency generators start simultaneously", Emergencies),
    ("Priority shedding and immediate restoration", Priority),
    ("Distance shedding and complete tie groups", DistanceAndTies),
    ("Weapon cadence, passive burst and loss reset", Weapons),
    ("Shield overflow and feedback on shield hits", Damage),
    ("Auto routing connects the full catalog", AutoRouting),
    ("Power islands never share generation or stored energy", Islands),
    ("Dash is serviced only while acting", Dash),
    ("Paused disconnection resets weapon cycles", PausedReset),
    ("Controllers idle when their components idle", IdleControllers),
    ("An empty battery still participates in distance ranking", BatteryDistance),
    ("Emergency backup waits for usable reserves, including exact boundary", BufferedEmergencies),
    ("Unavailable battery reserves cannot suppress emergency backup", UnavailableReserve),
    ("Alternate live controllers and commands survive preferred-controller loss", AlternateControllers),
    ("A shed controller switches to its live alternative", ShedControllerAlternative),
    ("Two live power islands retain independent generation and battery balances", LiveIslands),
    ("Feedback energy cannot cross power islands", FeedbackIslands),
    ("Paused move, rotation, upgrade and removal refresh without spending energy", RefreshedEditing),
    ("Tied loads stay online until shortage and restore together", TieRestoration),
    ("High-priority loads restore before low-priority loads without a battery", HighFirstRestoration),
    ("Mixed cable crossings require bridges and failed routes are atomic", MixedBridges),
    ("Tiny passive periods terminate and preserve weapon cadence", TinyDurations),
    ("Passive gaps begin at burst end and cannot bypass shot cooldown", BurstBoundaries),
    ("Charging pays controller cost before storing surplus", ChargingControlCost),
    ("Unavailable emergency control does not start or consume cooldown", UnavailableEmergency),
    ("Typed terminal contract and all rotated controller ports", TypedPorts),
    ("Wrong system draw and connect preserve the entire transaction", WrongSystemTransactions),
    ("Controllers require separate command input and system output", SeparateControllerNetworks),
    ("Mixed data bridge crossings stay isolated and cannot be merged", TypedBridgeCrossing),
    ("Add move and rotation reject incompatible terminal contacts atomically", TypedModuleEdits),
    ("Routes can pass alongside incompatible terminals without touching", PassAlongsideTerminal),
    ("Starter addon geometry can be routed and remains operational", StarterAddons),
    ("Main generator exposes only power and runs without data or controllers", GeneratorPowerOnly),
};
int failed = 0;
foreach (var (name, test) in tests)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failed++; Console.WriteLine($"FAIL {name}: {e}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
return failed == 0 ? 0 : 1;

static void Check(bool condition, string message = "Assertion failed")
{ if (!condition) throw new InvalidOperationException(message); }
static void Near(double expected, double actual, double tolerance = 1e-6)
{ Check(Math.Abs(expected - actual) <= tolerance, $"Expected {expected}, actual {actual}"); }
static ShipModule Of(ShipLayout layout, ModuleKind kind) => layout.Modules.First(m => m.Kind == kind);
static IReadOnlyList<Cell> Line(Cell a, Cell b)
{
    var cells = new List<Cell> { a };
    while (a != b) { a = new(a.X + Math.Sign(b.X - a.X), a.Y + Math.Sign(b.Y - a.Y)); cells.Add(a); }
    return cells;
}
static void Draw(ShipLayout layout, WireKind kind, Cell a, Cell b)
{ var result = layout.TryDrawWire(kind, Line(a, b)); Check(result.Success, result.Error); }
static ShipLayout Bus(params ModuleKind[] kinds)
{
    var layout = new ShipLayout(kinds.Length * 4 + 4, 18);
    for (int i = 0; i < kinds.Length; i++)
    {
        Check(layout.TryAdd(kinds[i], new(2 + i * 4, 2), out var module).Success);
        module!.Priority = module.Kind == ModuleKind.Command || module.Definition.IsController ? 10 : 5;
    }
    // Preserve the original power geometry: distance/tie and island assertions depend on it.
    Draw(layout, WireKind.Power, new(1, 0), new(layout.Width - 2, 0));
    foreach (var module in layout.Modules)
    {
        var power = module.Port(WireKind.Power).CableCell;
        Draw(layout, WireKind.Power, new(power.X, 0), power);
    }
    // Each data type has its own horizontal bus, with isolated perpendicular crossings.
    foreach (var kind in Enum.GetValues<WireKind>().Where(k => k != WireKind.Power))
    {
        var candidates = layout.Modules.Where(m => m.HasPort(kind)).ToArray();
        if (candidates.Length == 0) continue;
        int row = 5 + (int)kind * 2;
        var ports = candidates.Select(m => m.Port(kind).CableCell).ToArray();
        Check(layout.TryDrawWire(kind, Line(new(1, row), new(layout.Width - 2, row)), true).Success);
        foreach (var port in ports)
        {
            var result = layout.TryDrawWire(kind, Line(port, new(port.X, row)), true);
            Check(result.Success, result.Error);
        }
    }
    return layout;
}
static void Starter()
{
    var layout = ShipLayout.CreateStarter();
    Check(layout.Width == 18 && layout.Height == 12 && layout.Modules.Count == 9);
    var network = layout.BuildNetwork(WireKind.Power);
    foreach (var module in layout.Modules) Check(network.Connected(layout.Modules[0].Id, module.Id), $"Unwired {module.Kind}");
    var sim = new ShipSimulation(layout);
    Check(sim.Status.Values.All(s => s.Powered)); Near(120, sim.Energy); Near(32, sim.Generation);
    for (int i = 0; i < 5; i++) { sim.Refresh(); Check(sim.Step(0, new(LeftFire: true)).Shots.Count == 0); }
    Near(120, sim.Energy); Near(0, sim.Demand);
    var frame = sim.Step(.01, new(MoveRequested: true, LeftFire: true, RightFire: true));
    Check(frame.Shots.Count == 2 && frame.MoveSpeed == 240 && frame.CanSteer);
    Check(sim.Energy < 120); Near(100, sim.Hull);
}
static void Editing()
{
    var layout = new ShipLayout();
    Check(layout.TryAdd(ModuleKind.Rocket, new(2, 2), out var module).Success);
    Check(module!.Width == 2 && module.Height == 1);
    Check(layout.TryRotate(module.Id).Success);
    Check(module.Width == 1 && module.Height == 2);
    Check(module.Port(WireKind.Power).Facing == Direction.East);
    Check(!layout.TryMove(module.Id, new(17, 11)).Success);
    Check(module.Position == new Cell(2, 2));
    Check(!layout.TryDrawWire(WireKind.Power, new[] { new Cell(1, 2), new Cell(2, 2) }).Success);
    Check(layout.Wires.Count == 0);
    Check(layout.TryAdd(ModuleKind.Blaster, new(7, 2), out var second).Success);
    layout.ConfigureWeapon(module.Id, WeaponMode.Left); layout.ConfigureWeapon(second!.Id, WeaponMode.Left);
    Check(module.WeaponMode == WeaponMode.Unassigned && second.WeaponMode == WeaponMode.Left);
}
static void Bridges()
{
    foreach (var kind in Enum.GetValues<WireKind>())
    {
    var layout = new ShipLayout(12, 12);
    // Inward-facing ports at west/east/north/south of the crossing.
    var moduleKind = kind switch
    {
        WireKind.PowerData => ModuleKind.PowerControl,
        WireKind.WeaponData => ModuleKind.WeaponControl,
        WireKind.ShieldData => ModuleKind.ShieldControl,
        _ => ModuleKind.EngineControl
    };
    int offset = kind == WireKind.Power ? 0 : kind == WireKind.Control ? 1 : 2;
    layout.TryAdd(moduleKind, new(1, 5), out var west, 1 + offset);
    layout.TryAdd(moduleKind, new(9, 5), out var east, 3 + offset);
    layout.TryAdd(moduleKind, new(5, 1), out var north, 2 + offset);
    layout.TryAdd(moduleKind, new(5, 9), out var south, offset);
    Draw(layout, kind, new(2, 5), new(8, 5));
    Draw(layout, kind, new(5, 2), new(5, 8));
    var sim = new ShipSimulation(layout);
    Check(layout.BuildNetwork(kind).Connected(west!.Id, north!.Id));
    Check(layout.TrySetBridge(new(5, 5), true).Success);
    sim.Refresh();
    var graph = layout.BuildNetwork(kind);
    Check(!graph.Connected(west.Id, north.Id));
    Check(graph.Distance(west.Id, north.Id) == int.MaxValue);
    Check(graph.Connected(west.Id, east!.Id) && graph.Connected(north.Id, south!.Id));
    Check(!layout.TryDrawWire(kind, new[] { new Cell(4, 5), new Cell(5, 5), new Cell(5, 6) }).Success);
    Check(layout.TrySetBridge(new(5, 5), false).Success);
    sim.Refresh();
    Check(layout.BuildNetwork(kind).Connected(west.Id, north.Id));
    }
}
static void Dependencies()
{
    var layout = ShipLayout.CreateStarter();
    layout.Remove(Of(layout, ModuleKind.Command).Id);
    var sim = new ShipSimulation(layout);
    Near(32, sim.Generation);
    var frame = sim.Step(.1, new(MoveRequested: true, LeftFire: true));
    Check(frame.Shots.Count == 0 && frame.MoveSpeed == 0 && !frame.CanSteer);
    Near(0, sim.Demand);
    Check(sim.Status[Of(layout, ModuleKind.Battery).Id].State == ModuleState.NoControl);
}
static void Batteries()
{
    var layout = ShipLayout.CreateStarter(); var sim = new ShipSimulation(layout);
    layout.Remove(Of(layout, ModuleKind.Generator).Id);
    sim.Refresh(); Near(0, sim.Generation);
    Check(sim.Step(.1, new(MoveRequested: true)).MoveSpeed > 0);
    Check(sim.Energy < 120 && sim.Energy > 0);
    var battery = Of(layout, ModuleKind.Battery); double before = sim.Energy;
    layout.RemoveWire(battery.Port(WireKind.Power).CableCell, WireKind.Power);
    sim.Refresh(); sim.Step(.1, new(MoveRequested: true)); Near(before, sim.Energy);
    Check(sim.Status[Of(layout, ModuleKind.Engine).Id].Powered == false);
}
static void Emergencies()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.PowerControl, ModuleKind.EngineControl,
        ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Emergency, ModuleKind.Emergency);
    var sim = new ShipSimulation(layout); sim.Step(.01, new(MoveRequested: true));
    Near(122, sim.Generation);
    Check(layout.Modules.Where(m => m.Kind == ModuleKind.Emergency).All(m => sim.Status[m.Id].Active));
    sim.Step(3, new(MoveRequested: true));
    sim.Step(.1, new(MoveRequested: true)); Near(32, sim.Generation);
}
static void Priority()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Engine);
    var engines = layout.Modules.Where(m => m.Kind == ModuleKind.Engine).ToArray();
    engines[0].Priority = 1; engines[1].Priority = 5; engines[2].Priority = 8;
    var sim = new ShipSimulation(layout); sim.Step(.01, new(MoveRequested: true));
    Check(sim.Status[engines[0].Id].State == ModuleState.Shed);
    Check(sim.Status[engines[1].Id].Active && sim.Status[engines[2].Id].Active);
    Near(28, sim.Demand);
    layout.Upgrade(Of(layout, ModuleKind.Generator).Id); layout.Upgrade(Of(layout, ModuleKind.Generator).Id);
    sim.Step(.01, new(MoveRequested: true));
    Check(engines.All(e => sim.Status[e.Id].Active)); Near(0, sim.Energy);
}
static void DistanceAndTies()
{
    var layout = Bus(ModuleKind.Engine, ModuleKind.EngineControl, ModuleKind.Command, ModuleKind.Generator, ModuleKind.Engine, ModuleKind.Engine);
    var sim = new ShipSimulation(layout); sim.Step(.01, new(MoveRequested: true));
    var engines = layout.Modules.Where(m => m.Kind == ModuleKind.Engine).ToArray();
    Check(sim.Status[engines[0].Id].State == ModuleState.Shed, "Farthest engine must shed first");
    // Symmetric consumers have equal route length; the whole tied group must shed.
    var tied = Bus(ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Generator, ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Command, ModuleKind.EngineControl);
    var tiedEngines = tied.Modules.Where(m => m.Kind == ModuleKind.Engine).ToArray();
    // Outer pair ties for farthest, leaving the nearer two engines below generator capacity.
    var tiedSim = new ShipSimulation(tied); tiedSim.Step(.01, new(MoveRequested: true));
    Check(tiedSim.Status[tiedEngines[0].Id].State == ModuleState.Shed && tiedSim.Status[tiedEngines[3].Id].State == ModuleState.Shed);
    Check(tiedSim.Status[tiedEngines[1].Id].Active && tiedSim.Status[tiedEngines[2].Id].Active);
}
static void Weapons()
{
    var layout = ShipLayout.CreateStarter(); var sim = new ShipSimulation(layout); var weapon = Of(layout, ModuleKind.Blaster);
    var shots = sim.Step(.5, new(LeftFire: true)).Shots;
    Check(shots.Count == 4, $"Expected four blaster shots, got {shots.Count}"); Near(.42, shots[3].TimeOffset);
    layout.ConfigureWeapon(weapon.Id, WeaponMode.Passive, .3, .4); sim.Refill();
    shots = sim.Step(1.01, new()).Shots;
    Check(shots.Count == 6, $"Expected six burst shots, got {shots.Count}"); Near(.7, shots[3].TimeOffset);
    layout.ConfigureWeapon(weapon.Id, WeaponMode.Passive, 0, 100); sim.Refill();
    Check(sim.Step(.3, new()).Shots.Count == 3, "Duration zero ignores gap");
    var port = weapon.Port(WireKind.Power).CableCell;
    layout.RemoveWire(port, WireKind.Power); sim.Step(.02, new());
    Check(layout.TryConnect(weapon.Id, Of(layout, ModuleKind.Generator).Id, WireKind.Power).Success);
    sim.Refresh(); Check(sim.Step(.001, new()).Shots.Count == 1, "Power restoration must restart cycle immediately");
}
static void Damage()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Battery, ModuleKind.Command, ModuleKind.PowerControl,
        ModuleKind.ShieldControl, ModuleKind.Shield, ModuleKind.Feedback);
    var sim = new ShipSimulation(layout); sim.Step(4, new()); Near(70, sim.Shield);
    sim.SetEnergy(0); var hit = sim.Damage(10); Near(10, hit.Absorbed); Near(100, sim.Hull); Near(12, sim.Energy);
    hit = sim.Damage(80); Near(60, hit.Absorbed); Near(20, hit.HullDamage); Near(80, sim.Hull); Near(24, sim.Energy);
    sim.Step(.1, new()); Check(sim.Shield > 0, "Hits must not delay recharge");
    sim.Repair(); Near(100, sim.Hull);
}
static void AutoRouting()
{
    var layout = Bus(Enum.GetValues<ModuleKind>()); layout.ClearWires();
    var errors = layout.AutoConnectAll(); Check(errors.Count == 0, string.Join("; ", errors));
    foreach (var kind in Enum.GetValues<WireKind>())
    {
        var network = layout.BuildNetwork(kind);
        foreach (var module in layout.Modules)
            Check(network.ModuleIslands.ContainsKey(module.Id) == module.HasPort(kind),
                $"{module.Kind} must only belong to supported {kind} networks");
    }
    var sim = new ShipSimulation(layout); Check(sim.Status.Values.All(s => s.Powered));
}
static void Islands()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.Engine, ModuleKind.Battery, ModuleKind.PowerControl);
    var engine = Of(layout, ModuleKind.Engine); var battery = Of(layout, ModuleKind.Battery);
    layout.RemoveWire(new(12, 0), WireKind.Power);
    var sim = new ShipSimulation(layout); sim.SetBatteryEnergy(battery.Id, 0);
    sim.Step(.1, new(MoveRequested: true));
    Check(!sim.Status[engine.Id].Active, "Generation must not cross a broken cable"); Near(0, sim.Energy);
}
static void Dash()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.PowerControl, ModuleKind.Battery, ModuleKind.Dash);
    var sim = new ShipSimulation(layout); Near(0, sim.Demand);
    var frame = sim.Step(.01, new(DashRequested: true)); Check(frame.DashStarted && frame.DashActive);
    frame = sim.Step(.01, new(DashRequested: true)); Check(!frame.DashStarted && frame.DashActive);
    sim.Step(.2, new()); frame = sim.Step(.01, new(DashRequested: true)); Check(frame.DashStarted);
}
static void PausedReset()
{
    var layout = ShipLayout.CreateStarter(); var weapon = Of(layout, ModuleKind.Blaster);
    layout.ConfigureWeapon(weapon.Id, WeaponMode.Passive, .2, 1);
    var sim = new ShipSimulation(layout); sim.Step(.3, new());
    layout.RemoveWire(weapon.Port(WireKind.Power).CableCell, WireKind.Power); sim.Refresh();
    Check(sim.Status[weapon.Id].State == ModuleState.NoPower);
    Check(layout.TryConnect(weapon.Id, Of(layout, ModuleKind.Generator).Id, WireKind.Power).Success); sim.Refresh();
    Check(sim.Step(.001, new()).Shots.Count == 1);
}
static void IdleControllers()
{
    var layout = ShipLayout.CreateStarter(); var sim = new ShipSimulation(layout);
    sim.Step(.1, new()); Near(0, sim.Demand); Near(120, sim.Energy);
    Check(layout.Modules.Where(m => m.Definition.IsController || m.Kind == ModuleKind.Command).All(m => !sim.Status[m.Id].Active));
    sim.Step(.1, new(MoveRequested: true)); Near(16, sim.Demand);
    Check(sim.Status[Of(layout, ModuleKind.EngineControl).Id].Active);
    Check(!sim.Status[Of(layout, ModuleKind.PowerControl).Id].Active);
    Check(!sim.Status[Of(layout, ModuleKind.WeaponControl).Id].Active);
}
static void BatteryDistance()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.Engine,
        ModuleKind.PowerControl, ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Battery);
    var sim = new ShipSimulation(layout); sim.SetEnergy(0); sim.Step(.001, new(MoveRequested: true));
    var engines = layout.Modules.Where(m => m.Kind == ModuleKind.Engine).ToArray();
    Check(sim.Status[engines[0].Id].State == ModuleState.Shed, "Closest battery must count as a distance source even when empty");
    Check(sim.Status[engines[2].Id].Active);
}

static void BufferedEmergencies()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.PowerControl, ModuleKind.EngineControl,
        ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Emergency, ModuleKind.Emergency, ModuleKind.Battery);
    var sim = new ShipSimulation(layout);
    sim.Step(1, new(MoveRequested: true));
    Near(32, sim.Generation); Near(110, sim.Energy);
    Check(layout.Modules.Where(m => m.Kind == ModuleKind.Emergency).All(m => !sim.Status[m.Id].Active));
    sim.SetEnergy(.1); sim.Step(.01, new(MoveRequested: true));
    Near(32, sim.Generation); Near(0, sim.Energy);
    Check(layout.Modules.Where(m => m.Kind == ModuleKind.Engine).All(m => sim.Status[m.Id].Active));
    sim.Step(.01, new(MoveRequested: true)); Near(122, sim.Generation);
    Check(layout.Modules.Where(m => m.Kind == ModuleKind.Emergency).All(m => sim.Status[m.Id].Active));
}
static void UnavailableReserve()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.PowerControl, ModuleKind.EngineControl,
        ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Emergency, ModuleKind.Battery);
    var battery = Of(layout, ModuleKind.Battery);
    layout.RemoveWire(battery.Port(WireKind.PowerData).CableCell, WireKind.PowerData);
    var sim = new ShipSimulation(layout); sim.Step(.01, new(MoveRequested: true));
    Near(77, sim.Generation); Near(120, sim.Energy);
    Check(sim.Status[battery.Id].State == ModuleState.NoControl);
    // A preferred battery controller in a dead island must not make the reserve look usable.
    var dead = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.Engine,
        ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Battery, ModuleKind.PowerControl);
    var controller = Of(dead, ModuleKind.PowerControl);
    dead.RemoveWire(controller.Port(WireKind.Power).CableCell, WireKind.Power);
    var deadSim = new ShipSimulation(dead); deadSim.Step(.01, new(MoveRequested: true));
    Near(120, deadSim.Energy); Near(28, deadSim.Demand);
    Check(!deadSim.Status[Of(dead, ModuleKind.Battery).Id].Powered);
}
static void AlternateControllers()
{
    // Try both insertion orders to detect pruning that depends on layout iteration order.
    foreach (bool childFirst in new[] { false, true })
    {
        var kinds = childFirst
            ? new[] { ModuleKind.Engine, ModuleKind.Battery, ModuleKind.EngineControl, ModuleKind.PowerControl, ModuleKind.Command,
                ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.PowerControl }
            : new[] { ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.PowerControl, ModuleKind.Generator,
                ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.PowerControl, ModuleKind.Engine, ModuleKind.Battery };
        var layout = Bus(kinds);
        var preferred = new[] { ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.PowerControl }
            .Select(kind => Of(layout, kind)).ToArray();
        foreach (var module in preferred)
        {
            module.Priority = 10;
            layout.RemoveWire(module.Port(WireKind.Power).CableCell, WireKind.Power);
            layout.Modules.Last(m => m.Kind == module.Kind).Priority = 9;
        }
        var sim = new ShipSimulation(layout); sim.SetEnergy(10);
        sim.Step(.1, new(MoveRequested: true));
        Check(sim.Status[Of(layout, ModuleKind.Engine).Id].Active);
        Check(sim.Status[Of(layout, ModuleKind.Battery).Id].Active);
        Near(11.4, sim.Energy); Near(18, sim.Demand);
        foreach (var module in preferred) Check(!sim.Status[module.Id].Powered);
        foreach (var module in layout.Modules.Where(m => (m.Kind == ModuleKind.Command || m.Definition.IsController) && !preferred.Contains(m)))
            Check(sim.Status[module.Id].Active);
    }
}
static void ShedControllerAlternative()
{
    // The preferred controller is powered by a tiny isolated buffer that cannot service this step.
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.Engine, ModuleKind.EngineControl,
        ModuleKind.PowerControl, ModuleKind.Battery, ModuleKind.EngineControl);
    var controllers = layout.Modules.Where(m => m.Kind == ModuleKind.EngineControl).ToArray();
    controllers[0].Priority = 9; controllers[1].Priority = 10;
    layout.RemoveWire(new(20, 0), WireKind.Power);
    var sim = new ShipSimulation(layout); sim.SetEnergy(.001);
    sim.Step(.01, new(MoveRequested: true));
    Check(sim.Status[controllers[1].Id].State == ModuleState.Shed);
    Check(sim.Status[controllers[0].Id].Active && sim.Status[Of(layout, ModuleKind.Engine).Id].Active);
    Near(.001, sim.Energy); Near(16, sim.Demand);
}
static void LiveIslands()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.PowerControl, ModuleKind.EngineControl,
        ModuleKind.Battery, ModuleKind.Engine, ModuleKind.Battery, ModuleKind.Engine);
    var batteries = layout.Modules.Where(m => m.Kind == ModuleKind.Battery).ToArray();
    var engines = layout.Modules.Where(m => m.Kind == ModuleKind.Engine).ToArray();
    layout.RemoveWire(new(24, 0), WireKind.Power);
    var sim = new ShipSimulation(layout); sim.SetBatteryEnergy(batteries[0].Id, 10); sim.SetBatteryEnergy(batteries[1].Id, 10);
    sim.Step(.5, new(MoveRequested: true));
    Near(17, sim.Status[batteries[0].Id].StoredEnergy);
    Near(4, sim.Status[batteries[1].Id].StoredEnergy);
    Check(engines.All(m => sim.Status[m.Id].Active));
    sim.Step(.5, new(MoveRequested: true));
    Check(sim.Status[engines[0].Id].Active && !sim.Status[engines[1].Id].Active);
    Check(sim.Status[batteries[1].Id].StoredEnergy < .2);
    Check(sim.Status[batteries[0].Id].StoredEnergy > 17);
}
static void FeedbackIslands()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.PowerControl, ModuleKind.Battery,
        ModuleKind.Feedback, ModuleKind.Battery, ModuleKind.Generator);
    layout.RemoveWire(new(20, 0), WireKind.Power);
    var batteries = layout.Modules.Where(m => m.Kind == ModuleKind.Battery).ToArray();
    var sim = new ShipSimulation(layout); sim.SetEnergy(0);
    var damage = sim.Damage(1); Near(12, damage.FeedbackEnergy);
    Near(12, sim.Status[batteries[0].Id].StoredEnergy); Near(0, sim.Status[batteries[1].Id].StoredEnergy);
}
static void RefreshedEditing()
{
    var layout = ShipLayout.CreateStarter(); var sim = new ShipSimulation(layout);
    var weapon = Of(layout, ModuleKind.Blaster); var battery = Of(layout, ModuleKind.Battery);
    sim.Step(.03, new(LeftFire: true));
    double energy = sim.Energy, cooldown = sim.Status[weapon.Id].CooldownRemaining;
    for (int i = 0; i < 3; i++) { sim.Refresh(); sim.Step(0, new(LeftFire: true)); }
    Near(energy, sim.Energy); Near(cooldown, sim.Status[weapon.Id].CooldownRemaining);
    Check(layout.TryMove(weapon.Id, new(15, 9), 1).Success); sim.Refresh();
    Check(!sim.Status[weapon.Id].Powered); Near(energy, sim.Energy);
    Check(layout.TryRotate(weapon.Id).Success); sim.Refresh(); Near(energy, sim.Energy);
    layout.Upgrade(battery.Id); sim.Refresh(); Near(144, sim.Capacity); Near(energy, sim.Energy);
    layout.Remove(weapon.Id); sim.Refresh(); Check(!sim.Status.ContainsKey(weapon.Id));
    Check(sim.Step(0, new(LeftFire: true)).Shots.Count == 0);
}
static void TieRestoration()
{
    var layout = Bus(ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Generator, ModuleKind.Engine, ModuleKind.Engine,
        ModuleKind.Command, ModuleKind.EngineControl);
    var sim = new ShipSimulation(layout);
    var engines = layout.Modules.Where(m => m.Kind == ModuleKind.Engine).ToArray();
    int a = engines[0].Id, b = engines[3].Id;
    Near(sim.Status[a].SourceDistance, sim.Status[b].SourceDistance);
    sim.Step(.01, new(MoveRequested: true));
    Check(sim.Status[a].State == ModuleState.Shed && sim.Status[b].State == ModuleState.Shed);
    for (int i = 0; i < 2; i++) layout.Upgrade(Of(layout, ModuleKind.Generator).Id);
    sim.Step(.01, new(MoveRequested: true));
    Check(sim.Status[a].State == ModuleState.Shed && sim.Status[b].State == ModuleState.Shed,
        "Capacity for only one tied consumer must not break the tie arbitrarily");
    for (int i = 0; i < 2; i++) layout.Upgrade(Of(layout, ModuleKind.Generator).Id);
    sim.Refresh(); sim.Step(.01, new(MoveRequested: true));
    Check(engines.All(m => sim.Status[m.Id].Active)); Near(52, sim.Demand); Near(0, sim.Capacity);
}
static void HighFirstRestoration()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl,
        ModuleKind.Engine, ModuleKind.Engine, ModuleKind.Engine);
    var generator = Of(layout, ModuleKind.Generator);
    var engines = layout.Modules.Where(m => m.Kind == ModuleKind.Engine).ToArray();
    for (int i = 0; i < engines.Length; i++) engines[i].Priority = i + 1;
    var port = generator.Port(WireKind.Power).CableCell;
    layout.RemoveWire(port, WireKind.Power); var sim = new ShipSimulation(layout);
    sim.Step(.01, new(MoveRequested: true)); Check(engines.All(m => !sim.Status[m.Id].Powered));
    Draw(layout, WireKind.Power, new(port.X, 0), port);
    sim.Step(.01, new(MoveRequested: true));
    Check(sim.Status[engines[0].Id].State == ModuleState.Shed);
    Check(sim.Status[engines[1].Id].Active && sim.Status[engines[2].Id].Active);
    Near(0, sim.Energy); Near(0, sim.Capacity);
}
static void MixedBridges()
{
    var layout = new ShipLayout(12, 12);
    Draw(layout, WireKind.Power, new(1, 5), new(9, 5));
    int count = layout.Wires.Count, revision = layout.Revision;
    var route = Line(new(5, 1), new(5, 9));
    Check(!layout.TryDrawWire(WireKind.Control, route).Success);
    Check(layout.Wires.Count == count && layout.Revision == revision);
    Check(layout.TryDrawWire(WireKind.Control, route, true).Success);
    Check(layout.IsBridge(new(5, 5)));
    Check(!layout.TrySetBridge(new(5, 5), false).Success);
    Check(!layout.TryDrawWire(WireKind.Control, new[] { new Cell(5, 4), new Cell(5, 5), new Cell(6, 5) }).Success);
    layout.RemoveWire(new(5, 5), WireKind.Control);
    Check(layout.TrySetBridge(new(5, 5), false).Success);
}
static void TinyDurations()
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    foreach (var (duration, gap) in new[] { (0d, 0d), (0d, 10d), (2e-9, 2e-9), (1e-12, .1), (.1, 1e-12), (double.Epsilon, double.Epsilon), (double.MaxValue, double.MaxValue) })
    {
        var layout = ShipLayout.CreateStarter(); var weapon = Of(layout, ModuleKind.Blaster);
        Check(layout.ConfigureWeapon(weapon.Id, WeaponMode.Passive, duration, gap).Success);
        var sim = new ShipSimulation(layout); var shots = sim.Step(.5, new()).Shots;
        Check(shots.Count >= 1 && shots.Count <= 4, $"Invalid shot count {shots.Count} for {duration}/{gap}");
        for (int i = 1; i < shots.Count; i++) Check(shots[i].TimeOffset - shots[i - 1].TimeOffset >= .14 - 1e-8);
        Check(double.IsFinite(sim.Demand) && double.IsFinite(sim.Energy));
        if (duration == 0 || gap == 0) Check(shots.Count == 4);
    }
    Check(watch.Elapsed.TotalSeconds < 5, "Tiny bursts must not enumerate millions of irrelevant cycle edges");
}
static void BurstBoundaries()
{
    var layout = ShipLayout.CreateStarter(); var weapon = Of(layout, ModuleKind.Blaster);
    layout.ConfigureWeapon(weapon.Id, WeaponMode.Passive, .01, .04);
    var sim = new ShipSimulation(layout); var shots = sim.Step(.5, new()).Shots;
    Check(shots.Count == 4); Near(.15, shots[1].TimeOffset); Near(.3, shots[2].TimeOffset); Near(.45, shots[3].TimeOffset);
    layout.ConfigureWeapon(weapon.Id, WeaponMode.Passive, .28, .12); sim.Refill();
    shots = sim.Step(.7, new()).Shots;
    Check(shots.Count == 4, $"A shot exactly at the burst end must wait for the next burst: {shots.Count}");
    Near(.4, shots[2].TimeOffset); Near(.54, shots[3].TimeOffset);
}
static void ChargingControlCost()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.WeaponControl,
        ModuleKind.PowerControl, ModuleKind.Battery, ModuleKind.Blaster);
    var generator = Of(layout, ModuleKind.Generator);
    for (int i = 0; i < 9; i++) layout.Upgrade(generator.Id);
    layout.ConfigureWeapon(Of(layout, ModuleKind.Blaster).Id, WeaponMode.Left);
    var sim = new ShipSimulation(layout); sim.SetEnergy(0);
    var frame = sim.Step(1d / 60, new(LeftFire: true)); Check(frame.Shots.Count == 1);
    Near(89.6, sim.Generation); Near(88, sim.Demand);
    // The remaining 1.6/s cannot cover the power controller's 2/s charge-service cost.
    Near(0, sim.Energy); Check(!sim.Status[Of(layout, ModuleKind.PowerControl).Id].Active);
    sim.Step(1d / 60, new()); Check(sim.Energy > 0 && sim.Status[Of(layout, ModuleKind.PowerControl).Id].Active);
}
static void UnavailableEmergency()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.Engine,
        ModuleKind.Engine, ModuleKind.Engine, ModuleKind.PowerControl, ModuleKind.Emergency);
    var controller = Of(layout, ModuleKind.PowerControl); var emergency = Of(layout, ModuleKind.Emergency);
    var port = controller.Port(WireKind.Power).CableCell;
    layout.RemoveWire(port, WireKind.Power);
    var sim = new ShipSimulation(layout); sim.Step(4, new(MoveRequested: true));
    Near(32, sim.Generation); Near(0, sim.Status[emergency.Id].CooldownRemaining);
    Check(!sim.Status[emergency.Id].Powered);
    Draw(layout, WireKind.Power, new(port.X, 0), port); sim.Refresh();
    sim.Step(.01, new(MoveRequested: true)); Near(77, sim.Generation); Check(sim.Status[emergency.Id].Active);
}

static string Snapshot(ShipLayout layout) => $"{layout.Revision}|" +
    string.Join(";", layout.Modules.Select(m => $"{m.Id}:{m.Kind}:{m.Position}:{m.Rotation}")) + "|" +
    string.Join(";", layout.Wires.OrderBy(w => w.Cell.Y).ThenBy(w => w.Cell.X).ThenBy(w => w.Kind)) + "|" +
    string.Join(";", layout.Bridges.OrderBy(c => c.Y).ThenBy(c => c.X));

static void TypedPorts()
{
    Check(Enum.GetValues<WireKind>().SequenceEqual(new[] { WireKind.Power, WireKind.Control,
        WireKind.EngineData, WireKind.PowerData, WireKind.WeaponData, WireKind.ShieldData }));
    var layout = new ShipLayout();
    foreach (var kind in new[] { ModuleKind.EngineControl, ModuleKind.PowerControl, ModuleKind.WeaponControl, ModuleKind.ShieldControl })
    {
        Check(layout.TryAdd(kind, new(4, 4), out var module).Success);
        var controller = module!;
        Check(controller.DataInputKind == WireKind.Control && controller.DataOutputKind is not null);
        var output = controller.DataOutputKind!.Value;
        Check(Enum.GetValues<WireKind>().Count(controller.HasPort) == 3);
        for (int rotation = 0; rotation < 4; rotation++)
        {
            Check(controller.Port(WireKind.Power).Facing == Direction.North.Rotate(rotation));
            Check(controller.Port(WireKind.Control).Facing == Direction.West.Rotate(rotation));
            Check(controller.Port(output).Facing == Direction.South.Rotate(rotation));
            Check(controller.Port(output).CableCell != controller.Port(WireKind.Control).CableCell);
            Check(layout.TryRotate(controller.Id).Success);
        }
        layout.Remove(controller.Id);
    }
    Check(layout.TryAdd(ModuleKind.Engine, new(4, 4), out var engine).Success);
    Check(engine!.DataInputKind == WireKind.EngineData && engine.DataOutputKind is null && !engine.HasPort(WireKind.Control));
    bool threw = false;
    try { engine.Port(WireKind.Control); } catch (ArgumentException) { threw = true; }
    Check(threw);
    var command = layout.BuildNetwork(WireKind.Control);
    Check(command.IslandOf(engine.Id) == -1 && !command.Connected(engine.Id, engine.Id));
    Check(command.Distance(engine.Id, engine.Id) == int.MaxValue);
    layout.Remove(engine.Id);
    Check(layout.TryAdd(ModuleKind.Command, new(4, 4), out var root).Success);
    Check(root!.DataInputKind is null && root.DataOutputKind == WireKind.Control);
}
static void WrongSystemTransactions()
{
    var layout = new ShipLayout();
    Check(layout.TryAdd(ModuleKind.EngineControl, new(2, 2), out var engineController).Success);
    Check(layout.TryAdd(ModuleKind.Blaster, new(7, 2), out var weapon).Success);
    string before = Snapshot(layout);
    Check(!layout.TryConnect(engineController!.Id, weapon!.Id, WireKind.EngineData).Success);
    Check(Snapshot(layout) == before);
    Check(!layout.TryConnect(engineController.Id, weapon.Id, WireKind.Control).Success);
    Check(Snapshot(layout) == before);
    // Endpoint at the wrong terminal fails before any automatic crossing bridge is committed.
    Draw(layout, WireKind.Power, new(5, 3), new(5, 7));
    before = Snapshot(layout);
    Check(!layout.TryDrawWire(WireKind.EngineData, Line(new(2, 4), weapon.Port(WireKind.WeaponData).CableCell), true).Success);
    Check(Snapshot(layout) == before);
    Check(!layout.TryDrawWire(WireKind.Control, new[] { weapon.Port(WireKind.Power).CableCell }).Success);
    Check(Snapshot(layout) == before);
    // Incompatible cable types cannot meet along a shared edge either.
    var edges = new ShipLayout();
    Draw(edges, WireKind.EngineData, new(1, 4), new(4, 4));
    before = Snapshot(edges);
    Check(!edges.TryDrawWire(WireKind.WeaponData, Line(new(6, 4), new(4, 4)), true).Success);
    Check(Snapshot(edges) == before);
}
static void SeparateControllerNetworks()
{
    var layout = Bus(ModuleKind.Generator, ModuleKind.Command, ModuleKind.EngineControl, ModuleKind.Engine);
    var command = Of(layout, ModuleKind.Command); var controller = Of(layout, ModuleKind.EngineControl);
    var engine = Of(layout, ModuleKind.Engine); var sim = new ShipSimulation(layout);
    Check(sim.ControlNetwork.Connected(command.Id, controller.Id));
    Check(!sim.ControlNetwork.Connected(command.Id, engine.Id));
    Check(layout.BuildNetwork(WireKind.EngineData).Connected(controller.Id, engine.Id));
    Check(!layout.BuildNetwork(WireKind.EngineData).Connected(command.Id, engine.Id));
    Check(sim.Step(.01, new(MoveRequested: true)).MoveSpeed > 0);
    layout.RemoveWire(controller.Port(WireKind.Control).CableCell, WireKind.Control);
    Check(sim.Step(.01, new(MoveRequested: true)).MoveSpeed == 0);
    Check(sim.Status[engine.Id].State == ModuleState.NoControl);
    Check(layout.BuildNetwork(WireKind.EngineData).Connected(controller.Id, engine.Id));
    Check(layout.TryConnect(command.Id, controller.Id, WireKind.Control).Success);
    Check(sim.Step(.01, new(MoveRequested: true)).MoveSpeed > 0);
    layout.RemoveWire(controller.Port(WireKind.EngineData).CableCell, WireKind.EngineData);
    Check(sim.ControlNetwork.Connected(command.Id, controller.Id));
    Check(sim.Step(.01, new(MoveRequested: true)).MoveSpeed == 0);
    Check(sim.Status[engine.Id].State == ModuleState.NoControl);
    Check(!layout.TryConnect(command.Id, engine.Id, WireKind.Control).Success);
    // Removing the controller cannot let the command bus service its engine directly.
    layout.Remove(controller.Id);
    Check(sim.Step(.01, new(MoveRequested: true)).MoveSpeed == 0);
}
static void TypedBridgeCrossing()
{
    var layout = new ShipLayout(12, 12);
    Check(layout.TryAdd(ModuleKind.EngineControl, new(1, 5), out var west, 3).Success);
    Check(layout.TryAdd(ModuleKind.EngineControl, new(9, 5), out var east, 1).Success);
    Check(layout.TryAdd(ModuleKind.WeaponControl, new(5, 1), out var north).Success);
    Check(layout.TryAdd(ModuleKind.WeaponControl, new(5, 9), out var south, 2).Success);
    Draw(layout, WireKind.EngineData, new(2, 5), new(8, 5));
    var path = Line(new(5, 2), new(5, 8)); string before = Snapshot(layout);
    Check(!layout.TryDrawWire(WireKind.WeaponData, path).Success && before == Snapshot(layout));
    Check(layout.TryDrawWire(WireKind.WeaponData, path, true).Success);
    Check(layout.IsBridge(new(5, 5)));
    Check(layout.BuildNetwork(WireKind.EngineData).Connected(west!.Id, east!.Id));
    Check(layout.BuildNetwork(WireKind.WeaponData).Connected(north!.Id, south!.Id));
    Check(!layout.BuildNetwork(WireKind.EngineData).Connected(west.Id, north.Id));
    before = Snapshot(layout);
    Check(!layout.TrySetBridge(new(5, 5), false).Success && before == Snapshot(layout));
    Check(!layout.TryDrawWire(WireKind.ShieldData, path, true).Success && before == Snapshot(layout));
    Check(!layout.TryDrawWire(WireKind.EngineData, new[] { new Cell(4, 5), new Cell(5, 5), new Cell(5, 6) }, true).Success);
    Check(before == Snapshot(layout));
    layout.RemoveWire(new(5, 5), WireKind.WeaponData);
    Check(layout.TrySetBridge(new(5, 5), false).Success);
}
static void TypedModuleEdits()
{
    var layout = new ShipLayout();
    Check(layout.TryAdd(ModuleKind.WeaponControl, new(5, 5), out var former, 2).Success);
    Draw(layout, WireKind.WeaponData, new(5, 3), new(5, 4));
    layout.Remove(former!.Id);
    string before = Snapshot(layout);
    // The remaining south-facing arm at (5,4) would enter this engine's north-facing data terminal.
    Check(!layout.TryAdd(ModuleKind.Engine, new(5, 5), out var rejected, 2).Success);
    Check(rejected is null && before == Snapshot(layout));
    Check(layout.TryAdd(ModuleKind.Engine, new(9, 7), out var engine, 2).Success);
    Check(engine!.Id == former.Id + 1, "Failed adds must not consume module IDs");
    before = Snapshot(layout);
    Check(!layout.TryMove(engine.Id, new(5, 5)).Success && before == Snapshot(layout));

    var rotations = new ShipLayout();
    Check(rotations.TryAdd(ModuleKind.WeaponControl, new(6, 5), out var formerController, 3).Success);
    Draw(rotations, WireKind.WeaponData, new(7, 5), new(8, 5));
    rotations.Remove(formerController!.Id);
    Check(rotations.TryAdd(ModuleKind.EngineControl, new(6, 5), out var controller).Success);
    before = Snapshot(rotations);
    // Clockwise rotation points the command input north and the power port east into weapon data.
    Check(!rotations.TryRotate(controller!.Id).Success && before == Snapshot(rotations));
    // Counterclockwise rotation puts the engine output east into the same incompatible wire.
    Check(!rotations.TryMove(controller.Id, controller.Position, 3).Success && before == Snapshot(rotations));
}
static void PassAlongsideTerminal()
{
    var layout = new ShipLayout();
    Check(layout.TryAdd(ModuleKind.Blaster, new(5, 2), out var weapon).Success);
    Draw(layout, WireKind.EngineData, new(3, 4), new(7, 4));
    Check(!layout.BuildNetwork(WireKind.EngineData).ModuleIslands.ContainsKey(weapon!.Id));
    string before = Snapshot(layout);
    Check(!layout.TryDrawWire(WireKind.EngineData, new[] { new Cell(5, 4) }).Success);
    Check(before == Snapshot(layout));
}
static void StarterAddons()
{
    var layout = ShipLayout.CreateStarter();
    foreach (var (kind, position) in new (ModuleKind, Cell)[]
    {
        (ModuleKind.Shield, new(1, 9)), (ModuleKind.ShieldControl, new(5, 10)),
        (ModuleKind.Dash, new(8, 10)), (ModuleKind.Generator, new(15, 9))
    }) Check(layout.TryAdd(kind, position, out _).Success, $"Cannot place {kind} at {position}");
    var errors = layout.AutoConnectAll(); Check(errors.Count == 0, string.Join("; ", errors));
    var sim = new ShipSimulation(layout);
    Check(sim.Status.Values.All(s => s.Powered));
    var frame = sim.Step(.01, new(MoveRequested: true, DashRequested: true, LeftFire: true));
    Check(frame.MoveSpeed > 0 && frame.DashStarted && frame.Shots.Count == 1);
}

static void GeneratorPowerOnly()
{
    var layout = new ShipLayout();
    Check(layout.TryAdd(ModuleKind.Generator, new(3, 3), out var added).Success);
    var generator = added!;
    Check(generator.Definition.RequiredController is null);
    for (int rotation = 0; rotation < 4; rotation++)
    {
        Check(generator.DataInputKind is null && generator.DataOutputKind is null);
        Check(Enum.GetValues<WireKind>().Where(generator.HasPort).SequenceEqual(new[] { WireKind.Power }));
        Check(generator.Port(WireKind.Power).Facing == Direction.North.Rotate(rotation));
        foreach (var kind in Enum.GetValues<WireKind>().Where(k => k != WireKind.Power))
        {
            bool threw = false;
            try { generator.Port(kind); } catch (ArgumentException) { threw = true; }
            Check(threw, $"Main generator cannot expose a {kind} terminal");
            Check(layout.BuildNetwork(kind).IslandOf(generator.Id) == -1);
        }
        Check(layout.TryRotate(generator.Id).Success);
    }
    var independent = new ShipSimulation(layout);
    independent.Step(1, new());
    Near(32, independent.Generation);
    Check(independent.Status[generator.Id].Powered);

    var starter = ShipLayout.CreateStarter();
    var main = Of(starter, ModuleKind.Generator);
    Check(!starter.Wires.Any(w => w.Kind == WireKind.PowerData && w.Cell.Y == 3 && w.Cell.X < 5),
        "Starter must not retain the old data stub toward the main generator");
    string before = Snapshot(starter);
    Check(!starter.TryConnect(main.Id, Of(starter, ModuleKind.PowerControl).Id, WireKind.PowerData).Success);
    Check(before == Snapshot(starter));
    starter.Remove(Of(starter, ModuleKind.Command).Id);
    starter.Remove(Of(starter, ModuleKind.PowerControl).Id);
    var sim = new ShipSimulation(starter);
    sim.Step(.1, new());
    Near(32, sim.Generation);
    Check(sim.Status[main.Id].Powered);
    Check(sim.Status[Of(starter, ModuleKind.Battery).Id].State == ModuleState.NoControl);
}
