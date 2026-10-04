using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Main
{
    private async void StartGameSmoke()
    {
        try
        {
            ShowView("menu");await Capture("game-00-menu");
            StartRun(23);Flight.World.AutoSpawn=false;
            Require(!IsLaboratory && Run!.Level==1,"new run starts at one");
            Require(!_autoConnectButton.Visible && !_labConsole.Visible,"laboratory tools absent in survival");
            await KeyStroke(Key.Tab);Require(CurrentView=="arena","no manual level panel during combat");
            var initial=Flight.ShipPosition;
            Flight.AimOverride=initial-new Vector2(0,180);
            Flight.World.Spawn(EnemyKind.Basic,new(initial.X,initial.Y-170));
            Input.ActionPress("primary");
            await ToSignal(GetTree().CreateTimer(1.2),SceneTreeTimer.SignalName.Timeout);
            Input.ActionRelease("primary");
            Require(Run!.Kills>0 && Run.Materials>0 && Run.Experience>0,"actual blaster hits grant automatic rewards");
            for(int i=0;i<5;i++)Flight.World.Spawn((EnemyKind)i,new(Flight.Bounds.Position.X+160+i*180,Flight.Bounds.Position.Y+110+i%2*70));
            await ToSignal(GetTree().CreateTimer(1.5),SceneTreeTimer.SignalName.Timeout);
            await Capture("game-01-combat");
            ShowSettings();float time=Flight.World.Time;
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            Require(Flight.World.Time==time,"settings pause enemies and projectiles");ShowView("arena");
            foreach(var enemy in Flight.World.Enemies.ToArray())Flight.World.DamageEnemy(enemy,10000);
            UpdateRun();Require(CurrentView=="editor" && Run.PanelOpen && Run.Offers.Count==3,"kills trigger paused level shop");
            await Capture("game-02-shop");
            RequestReturnToFlight();Require(_closeDialogOpen,"unused upgrade requires confirmation");
            var confirmation=GetChildren().OfType<ConfirmationDialog>().Single();confirmation.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Require(Run.PanelOpen && Run.UpgradeAvailable && !_closeDialogOpen,"cancel preserves free upgrade");
            var generator=Layout.Modules.First(m=>m.Kind==ModuleKind.Generator);Grid.Select(generator.Id);UpdateInspector();
            int xp=Run.Experience,cash=Run.Materials;
            await Click(ButtonPoint("Ulepsz  +1"));
            Require(generator.Level==2 && !Run.UpgradeAvailable && Run.Experience==xp && Run.Materials==cash,"one upgrade is free through UI");
            Require(!Run.Upgrade(Layout,generator.Id),"second upgrade blocked");
            int offerIndex=Run.Offers.ToList().FindIndex(o=>!o.Sold && o.Price<=Run.Materials);
            Require(offerIndex>=0,"first level can afford a shop offer");var offer=Run.Offers[offerIndex];
            await Click(ButtonPoint($"{Art.Name(offer.Kind)}\n{offer.Price} M"));Require(offer.Sold,"shop UI purchases stock");
            var stock=Run.Inventory.First();
            await Click(ButtonPoint($"{Art.Name(stock.Kind)}  ·  {stock.Level}"));
            var cell=(from y in Enumerable.Range(0,Layout.Height) from x in Enumerable.Range(0,Layout.Width)
                let c=new Cell(x,y) where Layout.CanPlace(stock.Kind,c).Success select c).First();
            await Click(GridPoint(cell));Require(!Run.Inventory.Any(s=>s.StockId==stock.StockId),"place purchased module from inventory");
            await Click(GridPoint(cell),MouseButton.Right);Require(Run.Inventory.Any(s=>s.Kind==stock.Kind),"removal returns module to stock");
            Run.Reward(1200,200);
            int visits=0;
            while(Run.PanelOpen && Run.Level<6 && visits++<8)
            {
                if(Run.UpgradeAvailable)Run.Upgrade(Layout,generator.Id);
                FinishLevel(false);
            }
            Require(Layout.Width>=20 && Layout.Height>=14,"fifth level grows grid");
            await Hover(_gridScroll.GlobalPosition+_gridScroll.Size-new Vector2(8,8));
            await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);
            Require(_gridScroll.ScrollVertical>0,"edge hover scrolls grown grid");
            await Capture("game-03-grown-grid");
            // Resolve overflow levels before checking death, restart and laboratory isolation.
            while(Run.PanelOpen)FinishLevel(true);
            Require(CurrentView=="arena","overflow level panels eventually resume");
            Simulation.Damage(10000);UpdateRun();await Capture("game-04-death");
            Require(Run.Ended,"death ends run");
            StartRun(24);Require(Run.Level==1 && Run.Materials==0 && Run.Inventory.Count==0 && Layout.Modules.All(m=>m.Level==1),"restart loses all progression");
            StartLaboratory();Require(IsLaboratory && _autoConnectButton.Visible && !Flight.World.AutoSpawn,"laboratory preserved");
            ShowView("editor");PopulateCatalog("");
            Require(ModuleCatalog.All.Count==19,"all modules available");
            await Capture("game-05-laboratory");
            GD.Print("GAME SMOKE PASS: menu, real kills/rewards, pause, shop, free upgrade, confirmation, inventory, growth/scroll, death/reset and laboratory.");
            QuitCleanly();
        }
        catch(Exception error)
        {
            GD.PushError(error.ToString());try { await Capture("game-99-failure"); }catch { }
            QuitCleanly(1);
        }
    }
}
