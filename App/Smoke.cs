using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Main
{
    private async void StartSmoke()
    {
        try
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Require(GetWindow().Mode==Window.ModeEnum.Fullscreen,"fullscreen by default");
            Require(Flight.Bounds.Position.X==0 && Flight.Bounds.End.X==Size.X-324,"arena uses expanded viewport");
            await Capture("00-pelny-ekran");
            await KeyStroke(Key.F11);
            await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
            await ResizeWindow(new Vector2I(1440,900));
            ShowView("editor");
            Require(Grid.CellSize>48,"larger ship grid");
            await VerifyEditorShortcuts();
            Require(Layout.Modules.Count==9,"nine starter modules");
            Require(Simulation.Status.Values.All(s=>s.Powered),"connected starter");
            await Capture("01-stocznia");
            await Click(GridPoint(new Cell(1,1)));
            Require(Grid.SelectedId==Layout.Modules.First(m=>m.Kind==ModuleKind.Generator).Id,"mouse selects module");
            var generator=Layout.Find(Grid.SelectedId!.Value)!;
            await KeyStroke(Key.R);Require(generator.Rotation==1,"R rotates selected module");
            ResetShip();
            await Click(ButtonPoint("Generator główny"));
            Require(Grid.Pending==ModuleKind.Generator,"catalog button starts placement");
            await Click(GridPoint(new Cell(10,8)));
            Require(Layout.Modules.Count==10,"mouse places module");
            await Click(ButtonPoint("Połącz automatycznie"));
            Require(Simulation.Status.Values.All(s=>s.Powered),"auto-connect added generator");
            await KeyStroke(Key.Delete);Require(Layout.Modules.Count==9,"delete removes selection");
            ResetShip();
            Grid.BeginAdd(ModuleKind.Shield);
            await Click(GridPoint(new Cell(12,5)));
            Require(Layout.Modules.Count==11 && Layout.Modules.Any(m=>m.Kind==ModuleKind.ShieldControl),"shield includes controller");
            await Click(ButtonPoint("Połącz automatycznie"));
            Require(Simulation.Status.Values.All(s=>s.Powered),"shield and controller can be connected");
            Grid.BeginAdd(ModuleKind.Dash);
            await Click(GridPoint(new Cell(8,8)));
            await Click(ButtonPoint("Połącz automatycznie"));
            Require(Simulation.Status.Values.All(s=>s.Powered),"dash connected");
            Grid.Select(Layout.Modules.First(m=>m.Kind==ModuleKind.Shield).Id);
            await Capture("06-rozbudowany-statek");
            ShowView("arena");
            Flight.AimOverride=new Vector2(750,330);
            Input.ActionPress("right");
            var initialPosition=Flight.ShipPosition;
            await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);
            Input.ActionRelease("right");
            Require(Flight.ShipPosition.X>initialPosition.X+20,"keyboard movement");
            var dashStart=Flight.ShipPosition;
            Input.ActionPress("dash");
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Input.ActionRelease("dash");
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            Require(Flight.ShipPosition.DistanceTo(dashStart)>40,"dash toward cursor without movement key");
            ResetShip();Flight.AimOverride=new Vector2(750,330);
            int before=Flight.ShotsFired;
            Input.ActionPress("primary");Input.ActionPress("secondary");
            await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);
            Require(Flight.ShotsFired>before+10,"weapons fire");
            Require(Flight.ExplosionsCreated>0,"rocket reaches target");
            await Capture("02-arena");
            Input.ActionRelease("primary");Input.ActionRelease("secondary");
            TestDamage();Require(Simulation.Hull==75,"test damage button");
            Simulation.Repair();Require(Simulation.Hull==100,"repair");
            Simulation.SetEnergy(0);Simulation.Refill();Require(Simulation.Energy==Simulation.Capacity,"refill");
            ShowView("settings");await Capture("03-ustawienia");
            Controls.MusicTrack=1;_audio.PlayMusic();
            Controls.MusicTrack=0;_audio.PlayMusic();
            await Click(_bindingButtons["dash"].GetGlobalRect().GetCenter());Require(_capture=="dash","binding button enters capture");
            await KeyStroke(Key.Q);Require(Controls.Name("dash")=="Q" && _capture==null,"rebind accepts key through UI");
            Require(!Controls.Rebind("up",new InputEventKey { PhysicalKeycode=Key.Q,Pressed=true },out _),"duplicate binding rejected");
            var restored=new Bindings();Require(restored.Name("dash")=="Q","binding persisted");
            Controls.Reset();
            ShowView("editor");
            var selected=Layout.Modules.First(m=>m.Kind==ModuleKind.Blaster);Grid.Select(selected.Id);await Capture("04-inspektor");
            Require(Layout.ConfigureWeapon(selected.Id,WeaponMode.Passive,.25,.4).Success,"passive configuration");
            AfterEdit();await Capture("05-bron-pasywna");
            double energy=Simulation.Energy;
            await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
            Require(Simulation.Energy==energy,"editor pauses simulation");
            ResetShip();Require(Layout.Modules.Count==9,"reset starter");
            await ResizeWindow(new Vector2I(1152,720));
            Require(GetWindow().Size==new Vector2I(1152,720),"small window resize applied");
            await Capture("07-mniejsze-okno");
            await ResizeWindow(new Vector2I(1680,960));
            await Capture("08-szeroki-panel");
            ShowView("arena");await Capture("09-szeroka-arena");
            Require(Flight.Bounds.Size.X>1116,"widescreen expands flight area");
            GD.Print("SMOKE PASS: fullscreen, responsive layout, Q pickup/rotation/cancel, right-click deletion, binding migration, arena, weapons, energy and settings persistence.");
            QuitCleanly(0);
        }
        catch(Exception error)
        {
            GD.PushError(error.ToString());
            try { await Capture("99-failure"); } catch(Exception screenshotError) { GD.PushWarning(screenshotError.Message); }
            QuitCleanly(1);
        }
    }
    private async Task ResizeWindow(Vector2I size)
    {
        // Some window managers restore fullscreen windows as maximized first.
        GetWindow().Mode=Window.ModeEnum.Windowed;
        await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
        GetWindow().Size=size;
        await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
    }
    private Vector2 GridPoint(Cell cell)=>Grid.GlobalPosition+new Vector2(cell.X+.5f,cell.Y+.5f)*Grid.CellSize;
    private Vector2 ButtonPoint(string text)
    {
        Button Find(Node node)
        {
            foreach(var child in node.GetChildren())
            {
                if(child is Button b && b.Text==text && b.IsVisibleInTree())return b;
                var found=Find(child);if(found!=null)return found;
            }
            return null!;
        }
        return Find(this).GetGlobalRect().GetCenter();
    }
    private async Task Hover(Vector2 position)
    {
        position=GetViewport().GetStretchTransform()*position;
        Input.WarpMouse(position);
        Input.ParseInputEvent(new InputEventMouseMotion { Position=position,GlobalPosition=position });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private async Task VerifyEditorShortcuts()
    {
        var module=Layout.Modules.First(m=>m.Kind==ModuleKind.Blaster);
        Layout.Upgrade(module.Id);module.Priority=7;Layout.ConfigureWeapon(module.Id,WeaponMode.Passive,.6,.8);
        var original=module.Position;int wires=Layout.Wires.Count;
        await Hover(GridPoint(original));await KeyStroke(Key.Q);
        Require(Grid.MovingId==module.Id,"Q picks hovered module without click");
        await KeyStroke(Key.R);
        await Click(GridPoint(new Cell(1,1)));
        Require(Grid.MovingId==module.Id && module.Position==original && module.Rotation==0,"invalid drop preserves module and pickup");
        await KeyStroke(Key.Escape);
        Require(!Grid.HasPlacement && CurrentView=="editor" && module.Position==original && module.Rotation==0,"Escape cancels without opening settings");
        await Hover(GridPoint(original));await KeyStroke(Key.Q);
        await Click(GridPoint(new Cell(1,1)),MouseButton.Right);
        Require(!Grid.HasPlacement && Layout.Modules.Count==9,"right click cancels pickup without deleting underlying object");
        await Hover(GridPoint(original));await KeyStroke(Key.Q);await KeyStroke(Key.R);
        await Hover(GridPoint(new Cell(10,8)));await Capture("10-przenoszenie");
        await Click(GridPoint(new Cell(10,8)));
        Require(!Grid.HasPlacement && module.Position!=original && module.Rotation==1,"pickup rotates and places");
        Require(module.Level==2 && module.Priority==7 && module.WeaponMode==WeaponMode.Passive && module.BurstDuration==.6 && module.BurstGap==.8,"move preserves module identity and settings");
        Require(Layout.Wires.Count==wires,"move leaves existing cables intact");
        await Click(GridPoint(module.Position),MouseButton.Right);
        Require(Layout.Find(module.Id)==null,"right click removes module");
        ResetShip();
        Require(Layout.TryDrawWire(WireKind.Power,new[] { new Cell(2,9),new Cell(3,9),new Cell(4,9) }).Success,"setup cable");
        Require(Layout.TryDrawWire(WireKind.Control,new[] { new Cell(3,8),new Cell(3,9) },true).Success,"setup mixed bridge");
        await Click(GridPoint(new Cell(3,9)),MouseButton.Right);
        Require(Layout.WireAt(new(3,9),WireKind.Power)==null && Layout.WireAt(new(3,9),WireKind.Control)==null && !Layout.IsBridge(new(3,9)),"right click removes both cables and bridge");
        Require(Layout.WireAt(new(2,9),WireKind.Power)!=null,"right click only removes hovered cable tile");
        SetTool(EditorTool.Power);
        await Click(GridPoint(new Cell(1,1)),MouseButton.Right);
        Require(Layout.Modules.Count==8,"right click deletes in cable tool");
        ResetShip();SetTool(EditorTool.Select);
        await Hover(GridPoint(new Cell(1,1)));await KeyStroke(Key.Q);ShowView("arena");ShowView("editor");
        Require(!Grid.HasPlacement && Layout.Modules.Count==9,"leaving editor cancels pickup");
        Require(Controls.Rebind("pickup",new InputEventKey { PhysicalKeycode=Key.E,Pressed=true },out _),"pickup can be rebound");
        await Hover(GridPoint(new Cell(1,1)));await KeyStroke(Key.E);
        Require(Grid.MovingId!=null,"rebound pickup works");await KeyStroke(Key.Escape);
        Require(new Bindings().Name("pickup")=="E","pickup binding persists");Controls.Reset();
        // Existing preferences survive introduction of the new action.
        Controls.Rebind("dash",new InputEventKey { PhysicalKeycode=Key.Q,Pressed=true },out _);
        var legacy=new ConfigFile();legacy.Load("user://settings.cfg");legacy.EraseSectionKey("controls","pickup");legacy.Save("user://settings.cfg");
        Require(new Bindings().Name("dash")=="Q" && Controls.Name("pickup")=="Q","legacy bindings preserved across separate flight/editor contexts");
        Controls.Reset();
        legacy.Load("user://settings.cfg");legacy.SetValue("controls","menu",$"key:{(long)Key.Q}");
        legacy.EraseSectionKey("controls","pickup");legacy.Save("user://settings.cfg");
        var migrated=new Bindings();Require(migrated.Name("menu")=="Q" && migrated.Name("pickup")=="E","legacy menu shortcut retained without pickup conflict");
        Controls.Reset();ResetShip();
    }
    private static void Require(bool condition,string reason)
    { if(!condition)throw new InvalidOperationException("SMOKE FAIL: "+reason); }
    private async Task Click(Vector2 position,MouseButton button=MouseButton.Left)
    {
        position=GetViewport().GetStretchTransform()*position;
        Input.WarpMouse(position);
        Input.ParseInputEvent(new InputEventMouseMotion { Position=position,GlobalPosition=position });
        Input.ParseInputEvent(new InputEventMouseButton { Position=position,GlobalPosition=position,ButtonIndex=button,Pressed=true });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { Position=position,GlobalPosition=position,ButtonIndex=button,Pressed=false });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private async Task KeyStroke(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=true });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=false });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private async Task Capture(string name)
    {
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var folder=ProjectSettings.GlobalizePath("res://artifacts");System.IO.Directory.CreateDirectory(folder);
        var result=GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder,name+".png"));
        Require(result==Error.Ok,"screenshot "+name);
    }
}
