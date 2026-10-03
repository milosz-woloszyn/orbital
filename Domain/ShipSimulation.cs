namespace Orbital.Domain;

/// <summary>Pure ship simulation. Call Refresh after paused edits; Step owns all weapon and burst clocks.</summary>
public sealed class ShipSimulation
{
    private const double Epsilon = 1e-9;
    private sealed class Runtime
    {
        public double Energy, Shield, ShotCooldown, BurstTime, EmergencyRemaining, EmergencyCooldown, DashRemaining;
        public bool FeedbackHit;
        public WeaponMode Mode;
        public double BurstDuration, BurstGap;
    }
    private sealed class Plan
    {
        public readonly Dictionary<int, double> Loads = new();
        public readonly Dictionary<int, double> Production = new();
        public readonly HashSet<int> Acting = new();
        public readonly HashSet<int> Functional = new();
        public readonly Dictionary<int, List<ShipModule>> Batteries = new();
    }
    private readonly Dictionary<int, Runtime> runtime = new();
    private readonly Dictionary<int, ModuleStatus> status = new();
    private WireNetwork power = null!, control = null!;
    private int revision = -1;
    private readonly Dictionary<int, int> distances = new();
    public ShipSimulation(ShipLayout layout, double hullCapacity = 100)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!double.IsFinite(hullCapacity) || hullCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(hullCapacity));
        Layout = layout; HullCapacity = hullCapacity; Hull = hullCapacity; Refresh();
    }
    public ShipLayout Layout { get; }
    public IReadOnlyDictionary<int, ModuleStatus> Status => status;
    public IReadOnlyList<ShipModule> ActiveModules => Layout.Modules.Where(m => status.TryGetValue(m.Id, out var value) && value.Active).ToArray();
    public double Energy => Layout.Modules.Where(m => m.Kind == ModuleKind.Battery).Sum(m => runtime.GetValueOrDefault(m.Id)?.Energy ?? 0);
    public double Capacity => Layout.Modules.Sum(m => m.Definition.Capacity * m.UpgradeFactor);
    public double Generation { get; private set; }
    public double Demand { get; private set; }
    public double RequestedDemand { get; private set; }
    public double Hull { get; private set; }
    public double HullCapacity { get; }
    public double Shield => Layout.Modules.Where(m => m.Kind == ModuleKind.Shield).Sum(m => runtime.GetValueOrDefault(m.Id)?.Shield ?? 0);
    public double ShieldCapacity => Layout.Modules.Sum(m => m.Definition.ShieldCapacity * m.UpgradeFactor);
    public double MaxShield => ShieldCapacity;
    public bool IsDestroyed => Hull <= 0;
    public WireNetwork PowerNetwork { get { Synchronize(); return power; } }
    public WireNetwork ControlNetwork { get { Synchronize(); return control; } }

    /// <summary>Recomputes connectivity and idle status without advancing clocks or transferring energy.</summary>
    public void Refresh()
    {
        Synchronize();
        var enabled = StructurallyEligible();
        RemoveUnavailable(enabled);
        var plan = BuildPlan(enabled, new Dictionary<int, double>(), 0);
        Generation = plan.Production.Values.Sum(); Demand = 0; RequestedDemand = 0;
        SetStatus(enabled, new HashSet<int>(), plan);
        foreach (var module in Layout.Modules.Where(m => !status[m.Id].Powered))
        {
            var state = runtime[module.Id];
            state.ShotCooldown = 0; state.BurstTime = 0; state.DashRemaining = 0;
        }
    }

    public SimulationFrame Tick(double seconds, FrameIntent intent) => Step(seconds, intent);

    public SimulationFrame Step(double seconds, FrameIntent intent)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        ArgumentNullException.ThrowIfNull(intent);
        Synchronize();
        if (seconds == 0) { Refresh(); return Frame(Array.Empty<WeaponShot>(), false); }
        if (IsDestroyed) { Demand = 0; Generation = 0; return new(Array.Empty<WeaponShot>(), 0, 0, false, false, false); }
        var shots = new List<WeaponShot>();
        double elapsed = 0, used = 0, generated = 0, requested = 0;
        bool dashStarted = false, requestDash = intent.DashRequested;
        while (elapsed < seconds - Epsilon)
        {
            double dt = Math.Min(1.0 / 60, seconds - elapsed);
            // Only visit possible shot times, not every passive-cycle edge. Arbitrarily short
            // bursts may have millions of idle cycles between two shots at the base cadence.
            foreach (var module in Layout.Modules)
            {
                var state = runtime[module.Id];
                if (module.Definition.IsWeapon) Limit(NextShotDelay(module, state, intent));
                Limit(state.EmergencyRemaining); Limit(state.EmergencyCooldown); Limit(state.DashRemaining);
            }
            var requests = new Dictionary<int, double>();
            var dueWeapons = new HashSet<int>();
            bool mayStartDash = requestDash && runtime.Values.All(s => s.DashRemaining <= Epsilon);
            foreach (var module in Layout.Modules)
            {
                var state = runtime[module.Id]; var definition = module.Definition;
                if (module.Kind == ModuleKind.Engine && intent.MoveRequested) requests[module.Id] = definition.Consumption;
                if (module.Kind == ModuleKind.Dash && (state.DashRemaining > Epsilon || mayStartDash)) requests[module.Id] = definition.Consumption;
                if (module.Kind == ModuleKind.Shield && state.Shield < definition.ShieldCapacity * module.UpgradeFactor - Epsilon)
                    requests[module.Id] = definition.Consumption;
                if (definition.IsWeapon && NextShotDelay(module, state, intent) <= Epsilon)
                { requests[module.Id] = definition.ShotEnergy / dt; dueWeapons.Add(module.Id); }
            }
            var enabled = StructurallyEligible();
            // A ready backup may power its own controls, but a physically unavailable
            // controller/battery must not contribute a fictitious reserve to the decision.
            RemoveUnavailable(enabled, includeReadyEmergencies: true);
            var initial = BuildPlan(enabled, requests, dt);
            // Spend usable buffered energy first. If the requested step still cannot be
            // funded, all ready backups on that power island start together before shedding.
            var deficits = IslandDemand(initial).Where(pair => pair.Value * dt >
                    initial.Production.GetValueOrDefault(pair.Key) * dt + BatteryEnergy(initial, pair.Key) + Epsilon)
                .Select(pair => pair.Key).ToHashSet();
            var startingEmergencies = new List<int>();
            foreach (var module in Layout.Modules.Where(m => m.Kind == ModuleKind.Emergency && initial.Functional.Contains(m.Id)))
            {
                var state = runtime[module.Id];
                if (deficits.Contains(power.IslandOf(module.Id)) && state.EmergencyRemaining <= Epsilon && state.EmergencyCooldown <= Epsilon)
                {
                    state.EmergencyRemaining = module.Definition.Duration;
                    startingEmergencies.Add(module.Id);
                }
            }
            RemoveUnavailable(enabled);
            foreach (int id in startingEmergencies.Where(id => !enabled.Contains(id))) runtime[id].EmergencyRemaining = 0;
            var shed = new HashSet<int>();
            Plan plan;
            requested += initial.Loads.Values.Sum() * dt;
            while (true)
            {
                RemoveUnavailable(enabled);
                plan = BuildPlan(enabled, requests, dt);
                var deficient = IslandDemand(plan).Where(pair => pair.Value * dt >
                    plan.Production.GetValueOrDefault(pair.Key) * dt + BatteryEnergy(plan, pair.Key) + Epsilon).Select(pair => pair.Key).ToHashSet();
                if (deficient.Count == 0) break;
                bool removed = false;
                foreach (int island in deficient)
                {
                    var consumers = Layout.Modules.Where(m => power.IslandOf(m.Id) == island && plan.Loads.GetValueOrDefault(m.Id) > Epsilon).ToArray();
                    if (consumers.Length == 0) continue;
                    int priority = consumers.Min(m => m.Priority);
                    int distance = consumers.Where(m => m.Priority == priority).Max(m => distances[m.Id]);
                    foreach (var module in consumers.Where(m => m.Priority == priority && distances[m.Id] == distance))
                    { enabled.Remove(module.Id); shed.Add(module.Id); removed = true; }
                }
                if (!removed) break;
            }
            var loads = IslandDemand(plan);
            foreach (int island in plan.Production.Keys.Union(loads.Keys).Union(plan.Batteries.Keys))
                TransferEnergy(plan.Batteries.GetValueOrDefault(island)?.Where(m => plan.Acting.Contains(m.Id)).ToList(),
                    (plan.Production.GetValueOrDefault(island) - loads.GetValueOrDefault(island)) * dt);
            foreach (var module in Layout.Modules)
            {
                var state = runtime[module.Id];
                bool functional = plan.Functional.Contains(module.Id);
                bool acting = plan.Acting.Contains(module.Id);
                if (module.Definition.IsWeapon)
                {
                    if (!functional) { state.ShotCooldown = 0; state.BurstTime = 0; }
                    else
                    {
                        if (acting && dueWeapons.Contains(module.Id))
                        {
                            var definition = module.Definition;
                            shots.Add(new(module.Id, module.Kind, definition.Damage * module.UpgradeFactor,
                                definition.ProjectileSpeed, definition.BlastRadius, elapsed));
                            state.ShotCooldown = definition.ShotInterval;
                        }
                        state.ShotCooldown = Math.Max(0, state.ShotCooldown - dt);
                        if (module.WeaponMode == WeaponMode.Passive && module.BurstDuration > 0)
                            state.BurstTime = AdvanceBurst(module, state.BurstTime, dt);
                    }
                }
                if (module.Kind == ModuleKind.Shield && acting)
                    state.Shield = Math.Min(module.Definition.ShieldCapacity * module.UpgradeFactor,
                        state.Shield + module.Definition.ShieldRecharge * module.UpgradeFactor * dt);
                if (module.Kind == ModuleKind.Dash)
                {
                    if (mayStartDash && acting) { state.DashRemaining = module.Definition.Duration; dashStarted = true; }
                    state.DashRemaining = acting ? Math.Max(0, state.DashRemaining - dt) : 0;
                }
                if (state.EmergencyRemaining > Epsilon)
                {
                    state.EmergencyRemaining = Math.Max(0, state.EmergencyRemaining - dt);
                    if (state.EmergencyRemaining <= Epsilon) state.EmergencyCooldown = module.Definition.Cooldown;
                }
                else state.EmergencyCooldown = Math.Max(0, state.EmergencyCooldown - dt);
                state.FeedbackHit = false;
            }
            SetStatus(enabled, shed, plan);
            used += plan.Loads.Values.Sum() * dt;
            generated += plan.Production.Values.Sum() * dt;
            elapsed += dt; requestDash = false;
            void Limit(double remaining) { if (remaining > Epsilon) dt = Math.Min(dt, remaining); }
        }
        Demand = used / seconds; Generation = generated / seconds; RequestedDemand = requested / seconds;
        return Frame(shots.AsReadOnly(), dashStarted);
    }

    private SimulationFrame Frame(IReadOnlyList<WeaponShot> shots, bool dashStarted)
    {
        bool Powered(ShipModule module) => status.TryGetValue(module.Id, out var value) && value.Powered;
        double speed = Layout.Modules.Where(m => m.Kind == ModuleKind.Engine && Powered(m)).Sum(m => m.Definition.Speed * m.UpgradeFactor);
        double dashSpeed = Layout.Modules.Where(m => m.Kind == ModuleKind.Dash && Powered(m) && runtime[m.Id].DashRemaining > Epsilon)
            .Sum(m => m.Definition.Speed * m.UpgradeFactor);
        bool canSteer = Layout.Modules.Any(m => m.Kind == ModuleKind.EngineControl && Powered(m));
        return new(shots, speed, dashSpeed, dashSpeed > 0, dashStarted, canSteer);
    }

    private static double NextShotDelay(ShipModule module, Runtime state, FrameIntent intent)
    {
        double cooldown = Math.Max(0, state.ShotCooldown);
        if (intent.WeaponRequests?.Contains(module.Id) == true) return cooldown;
        if (module.WeaponMode != WeaponMode.Passive)
            return (module.WeaponMode == WeaponMode.Left && intent.LeftFire || module.WeaponMode == WeaponMode.Right && intent.RightFire)
                ? cooldown : double.PositiveInfinity;
        if (module.BurstDuration == 0 || module.BurstGap == 0) return cooldown;
        double phase = AdvanceBurst(module, state.BurstTime, cooldown);
        return phase < module.BurstDuration ? cooldown : cooldown + (module.BurstDuration + module.BurstGap - phase);
    }

    private static double AdvanceBurst(ShipModule module, double phase, double elapsed)
    {
        double cycle = module.BurstDuration + module.BurstGap;
        // Two individually valid settings may sum to infinity; their first burst still works.
        if (double.IsPositiveInfinity(cycle)) return phase + elapsed;
        phase = (phase + elapsed % cycle) % cycle;
        double tolerance = Math.Min(Epsilon, Math.Min(module.BurstDuration, module.BurstGap) * 1e-6);
        if (cycle - phase <= tolerance) return 0;
        if (Math.Abs(phase - module.BurstDuration) <= tolerance) return module.BurstDuration;
        return phase;
    }

    private void Synchronize()
    {
        if (revision == Layout.Revision) return;
        foreach (int id in runtime.Keys.Where(id => Layout.Find(id) is null).ToArray()) runtime.Remove(id);
        foreach (var module in Layout.Modules)
        {
            if (!runtime.TryGetValue(module.Id, out var state))
                runtime[module.Id] = state = new Runtime { Energy = module.Definition.Capacity * module.UpgradeFactor };
            state.Energy = Math.Min(state.Energy, module.Definition.Capacity * module.UpgradeFactor);
            state.Shield = Math.Min(state.Shield, module.Definition.ShieldCapacity * module.UpgradeFactor);
            if (state.Mode != module.WeaponMode || state.BurstDuration != module.BurstDuration || state.BurstGap != module.BurstGap)
            { state.ShotCooldown = 0; state.BurstTime = 0; state.Mode = module.WeaponMode; state.BurstDuration = module.BurstDuration; state.BurstGap = module.BurstGap; }
        }
        power = Layout.BuildNetwork(WireKind.Power); control = Layout.BuildNetwork(WireKind.Control);
        var sources = Layout.Modules.Where(m => m.Definition.IsEnergy).Select(m => m.Id).ToArray();
        distances.Clear();
        foreach (var module in Layout.Modules) distances[module.Id] = power.DistanceToNearest(module.Id, sources);
        revision = Layout.Revision;
    }

    private HashSet<int> StructurallyEligible()
    {
        var all = Layout.Modules.Select(m => m.Id).ToHashSet();
        return Layout.Modules.Where(m => Dependencies(m, all) is not null).Select(m => m.Id).ToHashSet();
    }

    private List<int>? Dependencies(ShipModule module, HashSet<int> enabled)
    {
        if (!enabled.Contains(module.Id)) return null;
        if (module.Definition.RequiredController is not { } kind) return new();
        foreach (var controller in Layout.Modules.Where(m => m.Kind == kind && enabled.Contains(m.Id) && control.Connected(module.Id, m.Id))
                     .OrderByDescending(m => m.Priority).ThenBy(m => m.Id))
        {
            var chain = Dependencies(controller, enabled);
            if (chain is not null) { chain.Add(controller.Id); return chain; }
        }
        return null;
    }

    private void RemoveUnavailable(HashSet<int> enabled, bool includeReadyEmergencies = false)
    {
        bool changed;
        do
        {
            changed = false;
            var supplied = Layout.Modules.Where(m => enabled.Contains(m.Id) && Dependencies(m, enabled) is not null &&
                (m.Kind == ModuleKind.Generator || m.Kind == ModuleKind.Battery && runtime[m.Id].Energy > Epsilon ||
                 m.Kind == ModuleKind.Emergency && (runtime[m.Id].EmergencyRemaining > Epsilon ||
                     includeReadyEmergencies && runtime[m.Id].EmergencyCooldown <= Epsilon)))
                .Select(m => power.IslandOf(m.Id)).ToHashSet();
            foreach (var module in Layout.Modules.Where(m => enabled.Contains(m.Id)))
                if (Dependencies(module, enabled) is null || !supplied.Contains(power.IslandOf(module.Id)))
                { enabled.Remove(module.Id); changed = true; }
        } while (changed);
    }

    private Plan BuildPlan(HashSet<int> enabled, Dictionary<int, double> requests, double dt)
    {
        var plan = new Plan();
        foreach (var module in Layout.Modules)
        {
            if (Dependencies(module, enabled) is null) continue;
            plan.Functional.Add(module.Id);
            int island = power.IslandOf(module.Id);
            if (module.Kind == ModuleKind.Generator || module.Kind == ModuleKind.Emergency && runtime[module.Id].EmergencyRemaining > Epsilon)
            {
                plan.Production[island] = plan.Production.GetValueOrDefault(island) + module.Definition.Generation * module.UpgradeFactor;
                plan.Acting.Add(module.Id);
                if (dt > 0) Service(module);
            }
            if (module.Kind == ModuleKind.Battery)
            {
                if (!plan.Batteries.TryGetValue(island, out var batteries)) plan.Batteries[island] = batteries = new();
                batteries.Add(module);
            }
            if (dt > 0 && requests.TryGetValue(module.Id, out double load))
            { plan.Loads[module.Id] = load; plan.Acting.Add(module.Id); Service(module); }
            if (dt > 0 && module.Kind == ModuleKind.Feedback && runtime[module.Id].FeedbackHit)
            { plan.Acting.Add(module.Id); Service(module); }
        }
        // Controller costs can themselves require battery discharge. Repeat until no service is added.
        if (dt > 0)
        {
            bool changed;
            do
            {
                int previousCount = plan.Acting.Count;
                foreach (var (island, batteries) in plan.Batteries)
                {
                    foreach (var module in batteries)
                    {
                        double balance = plan.Production.GetValueOrDefault(island) - plan.Loads.Where(p => power.IslandOf(p.Key) == island).Sum(p => p.Value);
                        double addedControlCost = (Dependencies(module, enabled) ?? new List<int>())
                            .Where(id => power.IslandOf(id) == island && !plan.Loads.ContainsKey(id)).Sum(id => Layout.Find(id)!.Definition.Consumption);
                        if (balance < -Epsilon && runtime[module.Id].Energy > Epsilon ||
                            balance > addedControlCost + Epsilon && runtime[module.Id].Energy < module.Definition.Capacity * module.UpgradeFactor - Epsilon)
                        { plan.Acting.Add(module.Id); Service(module); }
                    }
                }
                changed = previousCount != plan.Acting.Count;
            } while (changed);
        }
        return plan;
        void Service(ShipModule module)
        {
            foreach (int id in Dependencies(module, enabled) ?? new List<int>())
            {
                plan.Loads[id] = Layout.Find(id)!.Definition.Consumption;
                plan.Acting.Add(id);
            }
        }
    }

    private Dictionary<int, double> IslandDemand(Plan plan)
    {
        var result = new Dictionary<int, double>();
        foreach (var (id, load) in plan.Loads)
        {
            int island = power.IslandOf(id);
            result[island] = result.GetValueOrDefault(island) + load;
        }
        return result;
    }
    private double BatteryEnergy(Plan plan, int island) => plan.Batteries.TryGetValue(island, out var batteries)
        ? batteries.Sum(m => runtime[m.Id].Energy) : 0;
    private void TransferEnergy(List<ShipModule>? batteries, double amount)
    {
        if (batteries is null || Math.Abs(amount) <= Epsilon) return;
        // Proportional filling/discharge preserves per-module buffers and is independent of module IDs.
        double available = amount > 0 ? batteries.Sum(m => m.Definition.Capacity * m.UpgradeFactor - runtime[m.Id].Energy)
            : batteries.Sum(m => runtime[m.Id].Energy);
        if (available <= Epsilon) return;
        double fraction = Math.Min(1, Math.Abs(amount) / available);
        foreach (var module in batteries)
        {
            var state = runtime[module.Id];
            double portion = amount > 0 ? module.Definition.Capacity * module.UpgradeFactor - state.Energy : -state.Energy;
            state.Energy = Math.Clamp(state.Energy + portion * fraction, 0, module.Definition.Capacity * module.UpgradeFactor);
        }
    }

    private void SetStatus(HashSet<int> enabled, HashSet<int> shed, Plan plan)
    {
        status.Clear();
        var all = Layout.Modules.Select(m => m.Id).ToHashSet();
        foreach (var module in Layout.Modules)
        {
            ModuleState state; string reason;
            if (shed.Contains(module.Id)) { state = ModuleState.Shed; reason = "Odłączony: niedobór energii"; }
            else if (Dependencies(module, all) is null) { state = ModuleState.NoControl; reason = "Brak połączenia ze sterownikiem lub dowodzeniem"; }
            else if (module.Definition.RequiredController is not null && Dependencies(module, enabled.Append(module.Id).ToHashSet()) is null)
            { state = ModuleState.NoControl; reason = "Sterownik lub dowodzenie bez zasilania"; }
            else if (!enabled.Contains(module.Id)) { state = ModuleState.NoPower; reason = "Brak dostępnego źródła zasilania"; }
            else if (plan.Acting.Contains(module.Id)) { state = ModuleState.Active; reason = "Pracuje"; }
            else { state = ModuleState.Idle; reason = "Gotowy"; }
            var value = runtime[module.Id];
            status[module.Id] = new(module.Id, state, reason, power.IslandOf(module.Id), distances[module.Id],
                plan.Loads.GetValueOrDefault(module.Id), value.Energy, value.Shield,
                module.Kind == ModuleKind.Emergency ? value.EmergencyCooldown : value.ShotCooldown);
        }
    }

    public DamageResult ApplyDamage(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (revision != Layout.Revision) Refresh();
        if (amount == 0 || IsDestroyed) return new(0, 0, 0);
        double absorbed = Math.Min(amount, Shield), remaining = absorbed;
        foreach (var module in Layout.Modules.Where(m => m.Kind == ModuleKind.Shield))
        {
            var state = runtime[module.Id]; double take = Math.Min(remaining, state.Shield);
            state.Shield -= take; remaining -= take;
        }
        double hullDamage = Math.Min(Hull, amount - absorbed);
        Hull -= hullDamage;
        var enabled = status.Values.Where(s => s.Powered).Select(s => s.ModuleId).ToHashSet();
        double feedback = 0;
        foreach (var module in Layout.Modules.Where(m => m.Kind == ModuleKind.Feedback && enabled.Contains(m.Id)))
        {
            double produced = module.Definition.FeedbackEnergy * module.UpgradeFactor;
            feedback += produced; runtime[module.Id].FeedbackHit = true;
            var batteries = Layout.Modules.Where(m => m.Kind == ModuleKind.Battery && enabled.Contains(m.Id) && power.IslandOf(m.Id) == power.IslandOf(module.Id)).ToList();
            TransferEnergy(batteries, produced);
        }
        foreach (int id in status.Keys.ToArray())
            status[id] = status[id] with { StoredEnergy = runtime[id].Energy, Shield = runtime[id].Shield };
        return new(absorbed, hullDamage, feedback);
    }
    public DamageResult Damage(double amount) => ApplyDamage(amount);
    public void Refill() { Synchronize(); SetEnergy(Capacity); }
    public void SetEnergy(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Synchronize();
        double fraction = Capacity > 0 ? Math.Clamp(amount / Capacity, 0, 1) : 0;
        foreach (var module in Layout.Modules.Where(m => m.Kind == ModuleKind.Battery))
            runtime[module.Id].Energy = module.Definition.Capacity * module.UpgradeFactor * fraction;
        Refresh();
    }
    public void SetBatteryEnergy(int moduleId, double amount)
    {
        if (!double.IsFinite(amount) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Synchronize();
        if (Layout.Find(moduleId) is not { Kind: ModuleKind.Battery } module) throw new ArgumentException("To nie jest bateria.", nameof(moduleId));
        runtime[moduleId].Energy = Math.Min(amount, module.Definition.Capacity * module.UpgradeFactor); Refresh();
    }
    public void Repair() { Hull = HullCapacity; Refresh(); }
}
