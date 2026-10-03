using Godot;

namespace Orbital;

public sealed class Bindings
{
    public static readonly (string Id, string Name, Key Key, MouseButton Mouse)[] Defaults =
    [
        ("up", "Ruch w górę", Key.W, MouseButton.None),
        ("down", "Ruch w dół", Key.S, MouseButton.None),
        ("left", "Ruch w lewo", Key.A, MouseButton.None),
        ("right", "Ruch w prawo", Key.D, MouseButton.None),
        ("dash", "Zryw", Key.Space, MouseButton.None),
        ("primary", "Broń — kanał LPM", Key.None, MouseButton.Left),
        ("secondary", "Broń — kanał PPM", Key.None, MouseButton.Right),
        ("ship", "Panel statku", Key.Tab, MouseButton.None),
        ("menu", "Menu / pauza", Key.Escape, MouseButton.None)
    ];
    public float MusicVolume { get; set; } = .28f;
    public float EffectsVolume { get; set; } = .5f;
    public int MusicTrack { get; set; } = 1;
    public string SaveError { get; private set; } = "";

    public Bindings(bool load = true)
    { Reset(false); if(load) Load(); }
    public void Reset(bool save=true)
    {
        foreach(var (id,_,key,mouse) in Defaults)
        {
            if(!InputMap.HasAction(id)) InputMap.AddAction(id);
            Assign(id, mouse == MouseButton.None ? new InputEventKey { PhysicalKeycode=key } :
                new InputEventMouseButton { ButtonIndex=mouse });
        }
        if(save) Save();
    }
    private static void Assign(string id, InputEvent input)
    { InputMap.ActionEraseEvents(id); InputMap.ActionAddEvent(id,input); }
    public string Name(string id)
    {
        var ev=InputMap.ActionGetEvents(id)[0];
        return ev switch
        {
            InputEventMouseButton m => m.ButtonIndex switch
            { MouseButton.Left=>"LPM", MouseButton.Right=>"PPM", MouseButton.Middle=>"ŚPM", _=>$"Mysz {(int)m.ButtonIndex}" },
            InputEventKey k=>OS.GetKeycodeString(k.PhysicalKeycode == Key.None ? k.Keycode : k.PhysicalKeycode),
            _=>"—"
        };
    }
    public bool Rebind(string id, InputEvent input, out string message)
    {
        message="";
        InputEvent? clean=input switch
        {
            InputEventKey { Pressed:true, Echo:false } k => new InputEventKey { PhysicalKeycode=k.PhysicalKeycode == Key.None ? k.Keycode:k.PhysicalKeycode },
            InputEventMouseButton { Pressed:true } m when m.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown) => new InputEventMouseButton { ButtonIndex=m.ButtonIndex },
            _=>null
        };
        if(clean==null) return false;
        foreach(var (other,title,_,_) in Defaults)
            if(other!=id && Same(InputMap.ActionGetEvents(other)[0],clean))
            { message=$"To przypisanie jest zajęte: {title}. Zmień je najpierw."; return false; }
        Assign(id,clean); Save(); message=SaveError.Length>0?SaveError:"Przypisanie zapisane."; return true;
    }
    private static bool Same(InputEvent a,InputEvent b) => (a,b) switch
    {
        (InputEventKey x,InputEventKey y)=>x.PhysicalKeycode==y.PhysicalKeycode,
        (InputEventMouseButton x,InputEventMouseButton y)=>x.ButtonIndex==y.ButtonIndex,
        _=>false
    };
    private void Load()
    {
        var file=new ConfigFile(); if(file.Load("user://settings.cfg")!=Error.Ok) return;
        var loaded=new List<InputEvent>();
        foreach(var (id,_,_,_) in Defaults)
        {
            var value=file.GetValue("controls",id,"").AsString();
            var parts=value.Split(':');
            if(parts.Length!=2 || !long.TryParse(parts[1],out var code) || code<=0) { Reset(false); return; }
            InputEvent ev=parts[0]=="mouse" ? new InputEventMouseButton { ButtonIndex=(MouseButton)code } : new InputEventKey { PhysicalKeycode=(Key)code };
            if(loaded.Any(x=>Same(x,ev))) { Reset(false); return; }
            loaded.Add(ev); Assign(id,ev);
        }
        MusicVolume=Math.Clamp((float)file.GetValue("audio","music",.28f),0,1);
        EffectsVolume=Math.Clamp((float)file.GetValue("audio","effects",.5f),0,1);
        MusicTrack=Math.Clamp((int)file.GetValue("audio","track",1),0,1);
    }
    public void Save()
    {
        var file=new ConfigFile();
        foreach(var (id,_,_,_) in Defaults)
        {
            var ev=InputMap.ActionGetEvents(id)[0];
            file.SetValue("controls",id,ev is InputEventKey k ? $"key:{(long)k.PhysicalKeycode}" : $"mouse:{(int)((InputEventMouseButton)ev).ButtonIndex}");
        }
        file.SetValue("audio","music",MusicVolume); file.SetValue("audio","effects",EffectsVolume); file.SetValue("audio","track",MusicTrack);
        var error=file.Save("user://settings.cfg"); SaveError=error==Error.Ok?"":$"Nie udało się zapisać ustawień: {error}";
    }
}
