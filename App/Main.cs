using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Main : Control
{
    public ShipLayout Layout { get; private set; }=null!;
    public ShipSimulation Simulation { get; private set; }=null!;
    public Arena Flight { get; private set; }=null!;
    public ShipGrid Grid { get; private set; }=null!;
    public Bindings Controls { get; private set; }=null!;
    private Sound _audio=null!;
    private Control _arenaView=null!,_editorView=null!,_settingsView=null!;
    private Label _energy=null!,_generation=null!,_hull=null!,_shield=null!,_speed=null!,_flightStatus=null!,_bindingsHint=null!,_toast=null!;
    private Meter _energyBar=null!,_hullBar=null!,_shieldBar=null!;
    private Control _deadPanel=null!;
    private string _view="arena",_previousView="arena";
    private float _displayDemand,_updateTimer,_toastTime;
    public bool IsPaused=>_view!="arena";
    public string CurrentView=>_view;
    private bool _smoke;
    private bool _quitting;

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit=false;
        Theme=Style.CreateTheme(); Art.Load();
        _smoke=OS.GetCmdlineUserArgs().Contains("--smoke");
        Controls=new Bindings(!_smoke);
        if(_smoke)
        {
            Controls.MusicVolume=0;Controls.EffectsVolume=0;
            // Integration input should not depend on the desktop compositor's refresh rate.
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=60;
        }
        _audio=new Sound(this,Controls);
        ResetShip(false);
        BuildArena();BuildEditor();BuildSettings();
        foreach(var screen in new[] { _arenaView,_editorView,_settingsView })
            screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _toast=Style.Label(this,"",16,Size.Y-22,Size.X-32,22,12,Style.Amber,true);
        Style.Bottom(Style.StretchRight(_toast));
        ShowView("arena");
        if(_smoke) CallDeferred(nameof(StartSmoke));
    }
    public override void _Notification(int what)
    { if(what==NotificationWMCloseRequest)QuitCleanly(); }
    private async void QuitCleanly(int code=0)
    {
        if(_quitting)return;
        _quitting=true;_view="quitting";
        _audio.Stop();
        // Audio playback is released by its mixing thread, not by rendered-frame count.
        await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit(code);
    }
    private Control Screen()
    {
        var screen=Style.Rect(new Control(),0,0,1440,900);AddChild(screen);
        return screen;
    }
    private static void Header(Control parent,string tag,string title,string description)
    {
        Style.Label(parent,tag,26,16,900,22,12,Style.Cyan,true);
        Style.Label(parent,title,24,40,850,42,32);
        Style.Label(parent,description,26,84,1020,20,13,Style.Muted);
    }
    private void BuildArena()
    {
        _arenaView=Screen();
        Flight=new Arena
        {
            Size=new(1440,900),
            Tick=(dt,intent)=>Simulation.Step(dt,intent),Paused=()=>IsPaused,
            Dead=()=>Simulation.IsDestroyed,Shield=()=>(float)Simulation.Shield,Audio=_audio
        };
        _arenaView.AddChild(Flight);Flight.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Header(_arenaView,"ORBITAL  /  LABORATORIUM 01","Swobodny lot","");
        Style.Right(Style.Button(_arenaView,"Ustawienia",1136,24,140,46,ShowSettings));
        Style.Right(Style.Button(_arenaView,"Statek  ↗",1288,24,152,46,()=>ShowView("editor"),true));
        var console=Style.Right(Style.StretchBottom(Style.Group(_arenaView,1136,96,304,630)));
        var consoleBack=Style.PanelAt(console,0,0,304,630);Style.StretchBottom(consoleBack);
        Style.Label(console,"KONSOLA TESTOWA",20,20,260,26,13,Style.Cyan,true);
        Style.Label(console,"Twoje laboratorium",20,55,260,30,23);
        Style.Label(console,"Bez przeciwników i kosztów.\nWszystkie moduły w stoczni.",20,96,263,54,15,Style.Muted);
        Style.Button(console,"Zadaj obrażenia  −25",20,177,264,44,TestDamage);
        Style.Button(console,"Uzupełnij energię",20,233,264,44,()=>{ Simulation.Refill();Toast("Baterie naładowane."); });
        Style.Button(console,"Opróżnij baterie",20,289,264,44,()=>{ Simulation.SetEnergy(0);Toast("Baterie rozładowane — obserwuj zasilanie."); });
        Style.Button(console,"Napraw kadłub",20,345,264,44,()=>{ Simulation.Repair();Toast("Kadłub naprawiony."); });
        Style.Button(console,"Przywróć zestaw startowy",20,413,264,44,()=>ResetShip());
        _flightStatus=Style.Label(console,"",20,486,264,61,13,Style.Muted,true);

        var hud=Style.Bottom(Style.StretchRight(Style.Group(_arenaView,0,738,1440,122)));
        var power=Style.StretchRight(Style.Group(hud,0,0,666,122));
        Style.StretchRight(Style.PanelAt(power,0,0,666,122));
        Style.Label(power,"ZASILANIE",20,12,200,24,12,Style.Muted,true);
        _energy=Style.Label(power,"",20,39,360,34,27,Style.Green);
        _generation=Style.Right(Style.Label(power,"",379,34,267,48,13,Style.Muted,true));
        _energyBar=Style.Rect(new Meter { Tint=Style.Green },20,92,626,20);power.AddChild(_energyBar);Style.StretchRight(_energyBar);

        var hull=Style.Right(Style.Group(hud,682,0,215,122));
        Style.PanelAt(hull,0,0,215,122);Style.Label(hull,"KADŁUB",20,12,175,22,12,Style.Muted,true);
        _hull=Style.Label(hull,"",20,48,175,30,24);
        _hullBar=Style.Rect(new Meter { Tint=Style.Red },20,99,175,10);hull.AddChild(_hullBar);
        var shield=Style.Right(Style.Group(hud,913,0,215,122));
        Style.PanelAt(shield,0,0,215,122);Style.Label(shield,"OSŁONA",20,12,175,22,12,Style.Muted,true);
        _shield=Style.Label(shield,"",20,48,175,30,24);
        _shieldBar=Style.Rect(new Meter { Tint=Style.Cyan },20,99,175,10);shield.AddChild(_shieldBar);
        var drive=Style.Right(Style.Group(hud,1144,0,296,122));
        Style.PanelAt(drive,0,0,296,122);Style.Label(drive,"NAPĘD / PRĘDKOŚĆ",20,12,260,22,12,Style.Muted,true);
        _speed=Style.Label(drive,"",20,48,260,32,24);
        Style.Label(drive,"Środek statku = punkt trafienia",20,93,260,25,11,Style.Muted);
        _bindingsHint=Style.Bottom(Style.StretchRight(Style.Label(_arenaView,"",16,859,1408,20,11,Style.Muted,true)));
        _deadPanel=Style.PanelAt(_arenaView,333,297,450,195);
        Style.Label(_deadPanel,"KADŁUB ZNISZCZONY",24,20,400,36,26,Style.Red);
        Style.Label(_deadPanel,"To laboratorium. Możesz naprawić statek\nlub przywrócić jego konfigurację początkową.",24,66,400,52,15,Style.Muted);
        Style.Button(_deadPanel,"Napraw i wróć do lotu",24,133,402,43,()=>Simulation.Repair(),true);
        Flight.Resized+=()=>_deadPanel.Position=Flight.Bounds.GetCenter()-_deadPanel.Size/2;
    }
    public void TestDamage()
    {
        var result=Simulation.Damage(25);Flight.Hit();
        Toast($"Trafienie: osłona −{result.Absorbed:0}, kadłub −{result.HullDamage:0}, odzysk +{result.FeedbackEnergy:0} E.");
    }
    public void ResetShip(bool notify=true)
    {
        Layout=ShipLayout.CreateStarter();Simulation=new ShipSimulation(Layout);Simulation.Refill();Simulation.Refresh();
        Flight?.Reset(); Grid?.Cancel(); Grid?.Select(null);
        if(notify) { Toast("Przywrócono blaster, rakietę i podstawowe systemy.");AfterEdit(); }
    }
    public void ShowView(string view)
    {
        if(_view=="editor" && view!="editor")Grid.Cancel();
        _view=view;_arenaView.Visible=view=="arena";_editorView.Visible=view=="editor";_settingsView.Visible=view=="settings";
        Input.MouseMode=Input.MouseModeEnum.Visible;
        GetViewport().GuiReleaseFocus();
        if(view=="editor") { Simulation.Refresh();UpdateInspector();Grid.QueueRedraw(); }
    }
    public void Toast(string text)
    { if(_toast==null)return; _toast.Text=text;_toastTime=5; }
    public void AfterEdit()
    {
        Simulation.Refresh();UpdateInspector();Grid.QueueRedraw();UpdateEditorStats();
        if(!Grid.HasPlacement)UpdateToolHint();
    }
    public override void _Process(double delta)
    {
        if(_arenaView==null)return;
        float dt=(float)delta;_displayDemand=Mathf.Lerp(_displayDemand,(float)Simulation.Demand,1-Mathf.Exp(-dt*5));
        _updateTimer-=dt;
        if(_toastTime>0) { _toastTime-=dt;if(_toastTime<=0)_toast.Text=""; }
        if(_updateTimer>0)return;_updateTimer=.08f;
        _energy.Text=$"{Simulation.Energy:0} / {Simulation.Capacity:0} E";
        _generation.Text=$"+{Simulation.Generation:0.0} E/s produkcja\n−{_displayDemand:0.0} E/s zużycie";
        _energyBar.Value=(float)Simulation.Energy;_energyBar.Maximum=(float)Simulation.Capacity;
        _energyBar.Usage=_displayDemand;_energyBar.UsageMax=100;_energyBar.QueueRedraw();
        _hull.Text=$"{Simulation.Hull:0} / {Simulation.HullCapacity:0}";
        _hullBar.Value=(float)Simulation.Hull;_hullBar.Maximum=(float)Simulation.HullCapacity;_hullBar.QueueRedraw();
        _shield.Text=Simulation.MaxShield>0?$"{Simulation.Shield:0} / {Simulation.MaxShield:0}":"BRAK MODUŁU";
        _shield.AddThemeFontSizeOverride("font_size",Simulation.MaxShield>0?24:17);
        _shieldBar.Value=(float)Simulation.Shield;_shieldBar.Maximum=(float)Simulation.MaxShield;_shieldBar.QueueRedraw();
        _speed.Text=$"{Flight.Velocity.Length():0} px/s";
        int ready=Simulation.Status.Values.Count(s=>s.Powered);
        _flightStatus.Text=$"SYSTEMY  {ready:00} / {Layout.Modules.Count:00} GOTOWYCH\n"+(ready==Layout.Modules.Count?"ŁĄCZNOŚĆ  OK":"SPRAWDŹ PORTY W STOCZNI");
        _flightStatus.AddThemeColorOverride("font_color",ready==Layout.Modules.Count?Style.Green:Style.Amber);
        _deadPanel.Visible=Simulation.IsDestroyed;
        _bindingsHint.Text=$"RUCH {Controls.Name("up")}/{Controls.Name("left")}/{Controls.Name("down")}/{Controls.Name("right")}   •   ZRYW {Controls.Name("dash")}   •   BROŃ {Controls.Name("primary")} / {Controls.Name("secondary")}   •   STOCZNIA {Controls.Name("ship")}   •   MENU {Controls.Name("menu")}";
        if(_view=="editor") UpdateEditorStats();
    }
    public override void _UnhandledInput(InputEvent ev)
    {
        if(_capture!=null || ev is InputEventKey { Echo:true })return;
        if(_view=="editor" && Grid.HasPlacement && ev is InputEventKey { Pressed:true,Keycode:Key.Escape })
        { Grid.Cancel();UpdateToolHint();GetViewport().SetInputAsHandled();return; }
        if(ev.IsActionPressed("menu"))
        {
            if(_view=="settings") ShowView(_previousView);
            else if(_view=="editor" && Grid.HasPlacement) Grid.Cancel();
            else ShowSettings();
            GetViewport().SetInputAsHandled();return;
        }
        if(ev.IsActionPressed("ship") && _view!="settings")
        { ShowView(_view=="editor"?"arena":"editor");GetViewport().SetInputAsHandled();return; }
        if(_view!="editor" || GetViewport().GuiGetFocusOwner() is LineEdit)return;
        if(ev.IsActionPressed("pickup"))
        { Grid.PickUpHovered();GetViewport().SetInputAsHandled();return; }
        if(ev is InputEventKey { Pressed:true } key)
        {
            if(key.Keycode==Key.R) { Grid.RotateSelection();AfterEdit(); }
            if(key.Keycode==Key.Delete) { Grid.RemoveSelection();AfterEdit(); }
            if(key.Keycode==Key.Escape)Grid.Cancel();
            if(key.Keycode>=Key.Key1 && key.Keycode<=Key.Key5) SetTool((EditorTool)(key.Keycode-Key.Key1));
        }
    }
}

public partial class Meter : Control
{
    public float Value,Maximum=100,Usage,UsageMax=100;
    public Color Tint=Style.Green;
    public Meter() { MouseFilter=MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("07101b"));
        float ratio=Maximum<=0?0:Math.Clamp(Value/Maximum,0,1);
        DrawRect(new Rect2(0,0,Size.X*ratio,Size.Y),Tint.Darkened(.1f));
        for(float x=10;x<Size.X;x+=14)DrawLine(new(x,0),new(x,Size.Y),new Color("101f28"),2);
        if(Usage>0)
        {
            float x=Math.Clamp(Usage/UsageMax,0,1)*Size.X;
            DrawLine(new(x,0),new(x,Size.Y),Style.Amber,3);
            DrawCircle(new(x,2),3,Style.Amber);
        }
    }
}
