using System.Numerics;
using Orbital.Domain;

internal static class CombatTests
{
    public static readonly (string,Action)[] All=
    {
        ("Level overflow, free upgrade, confirmation and growth",Progression),
        ("Shop stock and inventory transactions preserve upgrades",Inventory),
        ("Shield purchase includes its controller",ShieldBundle),
        ("Carrier death rewards each escort exactly once",Carrier),
        ("Laser pierces and projectiles use swept collisions",Collisions),
        ("Fan, guided rockets, bombs and seeker retargeting",NewWeapons),
        ("All enemy archetypes attack and predicted rocket locks target",Enemies),
        ("Endurance combat remains finite with expired bullets",Endurance),
        ("All weapons expose the weapon data port",Ports),
        ("Inventory cannot refill batteries for free",Reserves),
        ("Close rocket targets detonate and dash has no collision immunity",CloseTargets),
        ("Every new weapon obeys ship energy and control",WeaponSystems),
        ("Dense mixed combat stays bounded",DenseCombat)
    };
    private static readonly ArenaBounds Bounds=new(0,0,1200,800);
    private static readonly Vector2 Player=new(600,650);
    private static void Check(bool value,string reason="check failed") { if(!value)throw new Exception(reason); }
    private static WeaponShot Shot(ModuleKind kind,double damage=0)
    { var d=ModuleCatalog.Get(kind);return new(1,kind,damage>0?damage:d.Damage,d.ProjectileSpeed,d.BlastRadius,0); }
    private static void Tick(CombatWorld world,float seconds,Vector2? cursor=null)
    { for(int i=0;i<(int)(seconds*60);i++)world.Step(1f/60,Player,Vector2.Zero,cursor??new(600,100),Array.Empty<WeaponShot>(),Bounds); }
    private static void Progression()
    {
        var layout=ShipLayout.CreateStarter();var run=new RunProgress(2);run.Reward(2000,300);
        Check(run.TryBeginLevel(layout));int xp=run.Experience,cash=run.Materials;
        Check(!run.TryBeginLevel(layout));Check(!run.ClosePanel());
        Check(run.Upgrade(layout,layout.Modules[0].Id));Check(!run.Upgrade(layout,layout.Modules[1].Id));
        Check(run.Experience==xp && run.Materials==cash);Check(run.ClosePanel());
        while(run.Level<5) { Check(run.TryBeginLevel(layout));Check(run.ClosePanel(true)); }
        Check(layout.Width==20 && layout.Height==14);Check(layout.Modules.Count==9);
        var sim=new ShipSimulation(layout);Check(sim.Status.Values.All(s=>s.Powered));
        run.End();run.Reward(10,10);Check(!run.TryBeginLevel(layout));Check(run.Materials==cash);
    }
    private static void Inventory()
    {
        var layout=ShipLayout.CreateStarter();var run=new RunProgress(4);run.Reward(50,1000);run.TryBeginLevel(layout);
        Check(run.Offers.Count==3);
        for(int i=0;i<3;i++) { Check(run.Buy(i).Success);Check(!run.Buy(i).Success); }
        var stock=run.Inventory[0];int count=run.Inventory.Count;
        Check(!run.Place(stock.StockId,layout,new(1,1),0,out _).Success);Check(count==run.Inventory.Count);
        var space=(from y in Enumerable.Range(0,layout.Height) from x in Enumerable.Range(0,layout.Width) let cell=new Cell(x,y) where layout.CanPlace(stock.Kind,cell).Success select cell).First();
        Check(run.Place(stock.StockId,layout,space,0,out var module).Success);
        module!.Priority=8;Check(run.Upgrade(layout,module.Id));
        Check(run.Store(layout,module.Id));var restored=run.Inventory.Last();
        Check(restored.Level==2 && restored.Priority==8);
        Check(run.Place(restored.StockId,layout,space,0,out var placed).Success);
        Check(placed!.Level==2 && placed.Priority==8);Check(!run.Upgrade(layout,placed.Id));
        Check(run.ClosePanel());Check(!run.Store(layout,placed.Id));
    }
    private static void ShieldBundle()
    {
        for(int seed=0;seed<100;seed++)
        {
            var run=new RunProgress(seed);var layout=ShipLayout.CreateStarter();run.Reward(45,1000);run.TryBeginLevel(layout);
            int index=run.Offers.ToList().FindIndex(o=>o.Kind==ModuleKind.Shield);
            if(index<0)continue;
            Check(run.Buy(index).Success);Check(run.Inventory.Count==2);
            Check(run.Inventory.Any(m=>m.Kind==ModuleKind.ShieldControl));return;
        }
        throw new Exception("Shield never offered");
    }
    private static void Carrier()
    {
        var world=new CombatWorld(1,false);int rewards=0;world.Rewarded+=_=>rewards++;
        var carrier=world.Spawn(EnemyKind.Carrier,new(500,200));Check(world.Enemies.Count==4);
        world.DamageEnemy(world.Enemies[1],1000);Check(rewards==1);
        world.DamageEnemy(carrier,1000);Check(rewards==4 && world.Kills==4);
        world.DamageEnemy(carrier,1000);Check(rewards==4);
        Tick(world,.1f);Check(world.Enemies.Count==0);
    }
    private static void Collisions()
    {
        Check(CombatWorld.SegmentHit(new(0,0),new(1000,0),new(500,0),5));
        var world=new CombatWorld(1,false);var a=world.Spawn(EnemyKind.Basic,new(600,400));var b=world.Spawn(EnemyKind.Basic,new(600,200));
        world.Step(.01f,Player,Vector2.Zero,new(600,0),new[] { Shot(ModuleKind.Laser,20) },Bounds);
        Check(a.Health<a.MaxHealth && b.Health<b.MaxHealth && world.Beams.Count==1);
        world.Step(.25f,Player,Vector2.Zero,new(600,0),new[] { new WeaponShot(2,ModuleKind.Blaster,100,2000,0,0) },Bounds);
        Check(!a.Alive && b.Alive,"bullet stops at first hit while laser pierces");
    }
    private static void NewWeapons()
    {
        var world=new CombatWorld(1,false);
        world.Step(.01f,Player,Vector2.Zero,new(600,100),new[] { Shot(ModuleKind.FanBlaster) },Bounds);Check(world.Bullets.Count==5);
        world=new CombatWorld(1,false);
        world.Step(.01f,Player,Vector2.Zero,new(600,100),new[] { Shot(ModuleKind.GuidedRocket) },Bounds);
        world.Step(.01f,Player,Vector2.Zero,new(900,600),Array.Empty<WeaponShot>(),Bounds);
        Check(world.Bullets[0].Target==new Vector2(900,600) && world.Bullets[0].Direction.X>0);
        world=new CombatWorld(1,false);float damage=0;world.PlayerDamaged+=d=>damage+=d;
        var near=world.Spawn(EnemyKind.Rocket,Player+new Vector2(90,0));near.FireTimer=100;
        world.Step(.01f,Player,Vector2.Zero,new(600,100),new[] { Shot(ModuleKind.Bomb) },Bounds);
        Check(world.Bullets[0].Position==Player);Tick(world,.7f);Check(world.Explosions==0);Tick(world,.2f);
        Check(world.Explosions==1 && damage==0 && !near.Alive);
        world=new CombatWorld(1,false);var first=world.Spawn(EnemyKind.Basic,new(600,400));var second=world.Spawn(EnemyKind.Basic,new(900,300));
        world.Step(.01f,Player,Vector2.Zero,new(600,100),new[] { Shot(ModuleKind.Seeker) },Bounds);
        Check(world.Bullets[0].TargetId==first.Id);world.DamageEnemy(first,1000);Tick(world,.02f);
        Check(world.Bullets[0].TargetId==second.Id);
        world.DamageEnemy(second,1000);Tick(world,10);Check(world.Bullets.Count==0);
    }
    private static void Enemies()
    {
        foreach(var kind in Enum.GetValues<EnemyKind>().Where(k=>k!=EnemyKind.Minion))
        {
            var world=new CombatWorld(2,false);var enemy=world.Spawn(kind,new(400,200));enemy.FireTimer=0;
            world.Step(.01f,Player,new(100,0),new(600,100),Array.Empty<WeaponShot>(),Bounds);
            Check(world.Bullets.Any(b=>b.Hostile),kind+" does not shoot");
            if(kind==EnemyKind.Rocket)
            {
                var bullet=world.Bullets[0];Check(bullet.Target.X>Player.X);var target=bullet.Target;
                world.Step(.1f,new(100,600),Vector2.Zero,Vector2.Zero,Array.Empty<WeaponShot>(),Bounds);
                Check(bullet.Target==target,"enemy rocket must keep prediction");
            }
        }
    }
    private static void Endurance()
    {
        var world=new CombatWorld(8);int rewards=0;world.Rewarded+=_=>rewards++;
        for(int i=0;i<60*180;i++)
        {
            float t=i/60f;var player=new Vector2(600+400*MathF.Sin(t*.55f),600+100*MathF.Cos(t*.7f));
            var aim=world.Nearest(player)?.Position??new Vector2(600,100);
            var shots=i%8==0?new[] { Shot(ModuleKind.FanBlaster,25),Shot(ModuleKind.Rocket,100) }:Array.Empty<WeaponShot>();
            world.Step(1f/60,player,Vector2.Zero,aim,shots,Bounds);
        }
        Check(rewards>10 && world.Enemies.Count<=94 && world.Bullets.Count<6100);
        Check(world.Bullets.All(b=>float.IsFinite(b.Position.X) && float.IsFinite(b.Position.Y)));
    }
    private static void Ports()
    {
        foreach(var d in ModuleCatalog.All.Where(d=>d.IsWeapon))
        {
            var layout=new ShipLayout();Check(layout.TryAdd(d.Kind,new(2,2),out var module).Success);
            Check(module!.DataInputKind==WireKind.WeaponData && d.RequiredController==ModuleKind.WeaponControl);
            Check(d.ShotInterval>0 && d.ShotEnergy>0 && d.Damage>0);
        }
    }
    private static void Reserves()
    {
        var layout=ShipLayout.CreateStarter();var simulation=new ShipSimulation(layout);simulation.SetEnergy(17);
        var run=new RunProgress(2);run.Reward(45,0);run.TryBeginLevel(layout);
        var battery=layout.Modules.First(m=>m.Kind==ModuleKind.Battery);var cell=battery.Position;
        Check(run.Store(layout,battery.Id,simulation));var stock=run.Inventory.Single();
        Check(run.Place(stock.StockId,layout,cell,0,out var restored,simulation).Success);
        Check(Math.Abs(simulation.Status[restored!.Id].StoredEnergy-17)<.001);
    }
    private static void CloseTargets()
    {
        var world=new CombatWorld(2,false);
        world.Step(.016f,Player,Vector2.Zero,Player,new[] { Shot(ModuleKind.Rocket) },Bounds);
        Check(world.Explosions==1 && world.Bullets.Count==0,"rocket at the cursor detonates immediately");
        world=new CombatWorld(2,false);float damage=0;world.PlayerDamaged+=d=>damage+=d;
        var enemy=world.Spawn(EnemyKind.Basic,new(600,300));enemy.FireTimer=0;
        world.Step(.01f,new(600,450),Vector2.Zero,Vector2.Zero,Array.Empty<WeaponShot>(),Bounds);
        world.Step(.2f,new(600,250),new(0,-1000),Vector2.Zero,Array.Empty<WeaponShot>(),Bounds);
        Check(damage>0,"moving across a projectile must collide, including a dash");
    }
    private static void WeaponSystems()
    {
        foreach(var kind in new[] { ModuleKind.GuidedRocket,ModuleKind.Laser,ModuleKind.FanBlaster,ModuleKind.Bomb,ModuleKind.Seeker })
        {
            var layout=new ShipLayout(32,18);
            var kinds=new[] { ModuleKind.Generator,ModuleKind.Command,ModuleKind.WeaponControl,ModuleKind.PowerControl,ModuleKind.Battery,kind };
            for(int i=0;i<kinds.Length;i++)Check(layout.TryAdd(kinds[i],new(2+i*4,2),out _).Success);
            Check(layout.AutoConnectAll().Count==0);
            var weapon=layout.Modules.Last();layout.ConfigureWeapon(weapon.Id,WeaponMode.Passive);
            var sim=new ShipSimulation(layout);var frame=sim.Step(.5,new());
            Check(frame.Shots.Any(s=>s.Kind==kind),kind+" did not fire");
            Check(sim.Energy<sim.Capacity || sim.Demand>0,"weapons must pay energy");
            layout.Remove(layout.Modules.First(m=>m.Kind==ModuleKind.Command).Id);
            Check(sim.Step(.5,new()).Shots.Count==0,"loss of command stops passive weapons");
        }
    }
    private static void DenseCombat()
    {
        var world=new CombatWorld(71,false);
        for(int i=0;i<50;i++)world.Spawn((EnemyKind)(i%5),new(60+(i%10)*115,90+(i/10)*55));
        int peak=0;var timer=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<1800;i++)
        {
            var player=new Vector2(600+420*MathF.Sin(i*.01f),650);
            world.Step(1f/60,player,Vector2.Zero,new(600,100),Array.Empty<WeaponShot>(),Bounds);
            peak=Math.Max(peak,world.Bullets.Count);
        }
        timer.Stop();Check(peak>300 && peak<6100);Check(world.Enemies.Count==80);
        Console.WriteLine($"  Dense combat: {world.Enemies.Count} enemies, peak {peak} bullets, 1800 steps in {timer.ElapsedMilliseconds} ms.");
    }
}
