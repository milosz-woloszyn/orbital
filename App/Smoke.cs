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
            ShowView("editor");
            Require(Layout.Modules.Count==9,"nine starter modules");
            Require(Simulation.Status.Values.All(s=>s.Powered),"connected starter");
            await Capture("01-stocznia");
            await Click(new Vector2(390,340));
            Require(Grid.SelectedId==Layout.Modules.First(m=>m.Kind==ModuleKind.Generator).Id,"mouse selects module");
            var generator=Layout.Find(Grid.SelectedId!.Value)!;
            await KeyStroke(Key.R);Require(generator.Rotation==1,"R rotates selected module");
            ResetShip();
            await Click(new Vector2(154,300));
            Require(Grid.Pending==ModuleKind.Generator,"catalog button starts placement");
            await Click(Grid.Position+new Vector2(10*48+24,8*48+24));
            Require(Layout.Modules.Count==10,"mouse places module");
            await Click(new Vector2(978,156));
            Require(Simulation.Status.Values.All(s=>s.Powered),"auto-connect added generator");
            await KeyStroke(Key.Delete);Require(Layout.Modules.Count==9,"delete removes selection");
            ResetShip();
            Grid.BeginAdd(ModuleKind.Shield);
            await Click(Grid.Position+new Vector2(12*48+24,5*48+24));
            Require(Layout.Modules.Count==11 && Layout.Modules.Any(m=>m.Kind==ModuleKind.ShieldControl),"shield includes controller");
            await Click(new Vector2(978,156));
            Require(Simulation.Status.Values.All(s=>s.Powered),"shield and controller can be connected");
            Grid.BeginAdd(ModuleKind.Dash);
            await Click(Grid.Position+new Vector2(8*48+24,8*48+24));
            await Click(new Vector2(978,156));
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
            await Click(new Vector2(683,469));Require(_capture=="dash","binding button enters capture");
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
            GD.Print("SMOKE PASS: arena, rockets, paused editor, inspector, damage, repair, energy, remapping and settings persistence.");
            QuitCleanly(0);
        }
        catch(Exception error)
        {
            GD.PushError(error.ToString());
            try { await Capture("99-failure"); } catch(Exception screenshotError) { GD.PushWarning(screenshotError.Message); }
            QuitCleanly(1);
        }
    }
    private static void Require(bool condition,string reason)
    { if(!condition)throw new InvalidOperationException("SMOKE FAIL: "+reason); }
    private async Task Click(Vector2 position)
    {
        Input.WarpMouse(position);
        Input.ParseInputEvent(new InputEventMouseMotion { Position=position,GlobalPosition=position });
        Input.ParseInputEvent(new InputEventMouseButton { Position=position,GlobalPosition=position,ButtonIndex=MouseButton.Left,Pressed=true });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { Position=position,GlobalPosition=position,ButtonIndex=MouseButton.Left,Pressed=false });
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
