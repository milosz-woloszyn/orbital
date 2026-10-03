using Godot;

namespace Orbital;

public partial class Main
{
    private string? _capture;
    private Label _captureMessage=null!;
    private readonly Dictionary<string,Button> _bindingButtons=new();
    private void BuildSettings()
    {
        _settingsView=Screen();
        Header(_settingsView,"ORBITAL  /  USTAWIENIA","Dopasuj swój kokpit","Przypisania i głośność są zapamiętywane. Układ statku startuje od nowa.");
        Style.Right(Style.Button(_settingsView,"Wróć",1288,35,152,46,()=>{ _capture=null;ShowView(_previousView); },true));
        Style.PanelAt(_settingsView,24,136,842,718);
        Style.Label(_settingsView,"STEROWANIE",48,155,540,30,14,Style.Cyan,true);
        Style.Label(_settingsView,"AKCJA",48,205,400,23,11,Style.Muted,true);
        Style.Label(_settingsView,"PRZYPISANIE",546,205,270,23,11,Style.Muted,true);
        for(int i=0;i<Bindings.Defaults.Length;i++)
        {
            var entry=Bindings.Defaults[i];int row=245+i*44;
            Style.Label(_settingsView,$"{i+1:00}",48,row+8,42,25,13,Style.Muted,true);
            Style.Label(_settingsView,entry.Name,105,row+5,410,30,17);
            _bindingButtons[entry.Id]=Style.Button(_settingsView,Controls.Name(entry.Id),546,row,294,40,()=>
            {
                _capture=entry.Id;UpdateBindingButtons();
                _captureMessage.Text=$"{entry.Name}: naciśnij klawisz lub przycisk myszy. Esc anuluje.";
                _captureMessage.AddThemeColorOverride("font_color",Style.Cyan);
            });
        }
        _captureMessage=Style.Label(_settingsView,"Kliknij przypisanie, aby je zmienić.",48,719,790,45,14,Style.Muted);
        _captureMessage.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        Style.Button(_settingsView,"Przywróć domyślne sterowanie",48,789,380,43,()=>
        { _capture=null;Controls.Reset();UpdateBindingButtons();_captureMessage.Text="Przywrócono i zapisano domyślne przypisania."; });
        Style.PanelAt(_settingsView,890,136,526,371);
        Style.Label(_settingsView,"DŹWIĘK",914,155,400,30,14,Style.Cyan,true);
        Style.Label(_settingsView,"Muzyka",914,207,270,28,17);
        var music=Style.Rect(new HSlider { MinValue=0,MaxValue=1,Step=.01,Value=Controls.MusicVolume },914,248,477,30);
        _settingsView.AddChild(music);
        music.ValueChanged+=value=>{ Controls.MusicVolume=(float)value;_audio.Apply();Controls.Save(); };
        Style.Label(_settingsView,"Efekty",914,303,270,28,17);
        var effects=Style.Rect(new HSlider { MinValue=0,MaxValue=1,Step=.01,Value=Controls.EffectsVolume },914,342,477,30);
        _settingsView.AddChild(effects);
        effects.ValueChanged+=value=>{ Controls.EffectsVolume=(float)value;Controls.Save(); };
        var track=Style.Rect(new OptionButton(),914,421,477,44);
        track.AddItem("Orbital Assault  /  138 BPM");track.AddItem("Orbital Overdrive  /  150 BPM");track.Select(Controls.MusicTrack);
        _settingsView.AddChild(track);track.ItemSelected+=index=>{ Controls.MusicTrack=(int)index;Controls.Save();_audio.PlayMusic(); };
        Style.PanelAt(_settingsView,890,529,526,325);
        Style.Label(_settingsView,"SEMI_GOAL  /  WERSJA TESTOWA",914,550,480,25,13,Style.Amber,true);
        Style.Label(_settingsView,"Swobodnie sprawdzaj konfiguracje.\nModuły i ulepszenia nic nie kosztują.\nPrzeciwnicy pojawią się w kolejnym etapie.",914,595,476,86,17,Style.Muted);
        Style.Label(_settingsView,"F11  pełny ekran\nEsc  anuluje oczekiwanie na przypisanie",914,704,478,47,13,Style.Muted,true);
        Style.Button(_settingsView,"Zakończ grę",914,789,477,43,()=>QuitCleanly());
        foreach(var child in _settingsView.GetChildren().OfType<Control>().Where(c=>c.Position.Y>=136))
        {
            if(child.Position.X>=890) { child.Position+=new Vector2(24,0);Style.Right(child); }
            else if(child is Panel) { child.Position-=new Vector2(24,0);child.Size+=new Vector2(24,0);Style.StretchRight(child); }
            else if(child.Position.X>=546)Style.Right(child);
            else child.Position-=new Vector2(24,0);
            if(child is Panel && child.Position.Y+child.Size.Y>=854) { child.OffsetBottom+=24;Style.StretchBottom(child); }
        }
    }
    private void ShowSettings()
    { _previousView=_view;_capture=null;UpdateBindingButtons();ShowView("settings"); }
    private void UpdateBindingButtons()
    {
        foreach(var (id,button) in _bindingButtons)
        {
            button.Text=_capture==id?"Oczekiwanie…":Controls.Name(id);
            button.AddThemeStyleboxOverride("normal",Style.Box(_capture==id?new Color("23404b"):Style.Panel,_capture==id?Style.Cyan:Style.Border));
        }
    }
    public override void _Input(InputEvent ev)
    {
        if(_capture!=null)
        {
            if(ev is InputEventKey { Pressed:true,Keycode:Key.Escape })
            { _capture=null;UpdateBindingButtons();_captureMessage.Text="Anulowano zmianę przypisania.";GetViewport().SetInputAsHandled();return; }
            if(ev is InputEventKey { Pressed:true, Echo:false } || ev is InputEventMouseButton { Pressed:true })
            {
                bool success=Controls.Rebind(_capture,ev,out var message);
                if(success)_capture=null;
                _captureMessage.Text=message;_captureMessage.AddThemeColorOverride("font_color",success?Style.Green:Style.Amber);
                UpdateBindingButtons();GetViewport().SetInputAsHandled();
            }
            return;
        }
        if(ev is InputEventKey { Pressed:true,Echo:false,Keycode:Key.F11 })
        {
            GetWindow().Mode=GetWindow().Mode==Window.ModeEnum.Fullscreen?Window.ModeEnum.Windowed:Window.ModeEnum.Fullscreen;
            GetViewport().SetInputAsHandled();
        }
    }
}
