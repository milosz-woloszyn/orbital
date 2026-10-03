using Godot;
using Orbital.Domain;

namespace Orbital;

public static class Art
{
    private static Texture2D _base=null!, _energy=null!, _control=null!;
    public static Texture2D Player { get; private set; }=null!;
    private static readonly Dictionary<ModuleKind, Texture2D> Icons=new();
    public static void Load()
    {
        _base=GD.Load<Texture2D>("res://graphics/sprites-base-v01.png");
        _energy=GD.Load<Texture2D>("res://graphics/moduly-v02/energia-i-naped.png");
        _control=GD.Load<Texture2D>("res://graphics/moduly-v02/sterowanie-bron-oslony.png");
        Player=new AtlasTexture { Atlas=_base, Region=new Rect2(45,90,230,235), FilterClip=true };
        var energyKinds=new[] { ModuleKind.Generator,ModuleKind.Battery,ModuleKind.Emergency,ModuleKind.Feedback,ModuleKind.Engine,ModuleKind.Dash };
        Rect2[] energyRegions=[new(90,65,400,365),new(630,65,300,365),new(1065,80,425,355),new(64,552,435,315),new(615,527,332,352),new(1020,566,495,295)];
        for(int i=0;i<energyKinds.Length;i++)
            Icons[energyKinds[i]]=new AtlasTexture { Atlas=_energy, Region=energyRegions[i], FilterClip=true };
        var controlKinds=new[] { ModuleKind.Command,ModuleKind.EngineControl,ModuleKind.PowerControl,ModuleKind.WeaponControl,ModuleKind.Blaster,ModuleKind.Rocket,ModuleKind.Shield };
        Rect2[] controlRegions=[new(48,40,365,301),new(456,75,398,256),new(933,30,320,300),new(1355,61,350,273),new(63,470,336,273),new(452,500,405,254),new(925,454,344,288)];
        for(int i=0;i<controlKinds.Length;i++)
            Icons[controlKinds[i]]=new AtlasTexture { Atlas=_control, Region=controlRegions[i], FilterClip=true };
        Icons[ModuleKind.ShieldControl]=Icons[ModuleKind.Shield];
    }
    public static Texture2D Icon(ModuleKind kind)=>Icons[kind];
    public static string Name(ModuleKind kind)=>kind switch
    {
        ModuleKind.Generator=>"Generator główny", ModuleKind.Battery=>"Bateria",
        ModuleKind.Emergency=>"Generator awaryjny",ModuleKind.Feedback=>"Sprzężenie zwrotne",
        ModuleKind.Engine=>"Silniki",ModuleKind.Dash=>"Silniki zrywu",ModuleKind.Command=>"Dowodzenie",
        ModuleKind.EngineControl=>"Sterownik silników",ModuleKind.PowerControl=>"Sterownik zasilania",
        ModuleKind.WeaponControl=>"Sterownik uzbrojenia",ModuleKind.ShieldControl=>"Sterownik osłon",
        ModuleKind.Blaster=>"Blaster",ModuleKind.Rocket=>"Rakieta",ModuleKind.Shield=>"Osłony",_=>kind.ToString()
    };
    public static string Description(ModuleKind kind)=>kind switch
    {
        ModuleKind.Generator=>"Niezależne źródło energii.\nNie wymaga sterownika.",
        ModuleKind.Battery=>"Zwiększa zapas energii.\nWymaga sterowania zasilaniem.",
        ModuleKind.Emergency=>"Automatyczny impuls mocy\nprzy wyczerpaniu zapasu.",
        ModuleKind.Feedback=>"Odzyskuje energię z trafień,\ntakże przy aktywnej osłonie.",
        ModuleKind.Engine=>"Napęd i kontrola ruchu.\nKolejne zwiększają prędkość.",
        ModuleKind.Dash=>"Krótki zryw za energię.\nBez nietykalności.",
        ModuleKind.Command=>"Sygnał dla sterowników.\nBez niego tracisz kontrolę.",
        ModuleKind.EngineControl=>"Steruje napędem i zrywem.\nWymaga dowodzenia.",
        ModuleKind.PowerControl=>"Steruje bateriami i dodatkowymi\nźródłami. Wymaga dowodzenia.",
        ModuleKind.WeaponControl=>"Steruje bronią aktywną\ni pasywną. Wymaga dowodzenia.",
        ModuleKind.ShieldControl=>"Steruje ładowaniem osłon.\nDodawany razem z osłoną.",
        ModuleKind.Blaster=>"Szybki strumień pocisków.\nNiski koszt pojedynczego strzału.",
        ModuleKind.Rocket=>"Zapamiętuje pozycję kursora.\nWybucha po dotarciu do celu.",
        ModuleKind.Shield=>"Ładuje osłonę za energię.\nObrażenia nadmiarowe trafiają w kadłub.",_=>""
    };
    public static string WireName(WireKind kind)=>kind switch
    {
        WireKind.Power=>"Zasilanie",WireKind.Control=>"Dowodzenie",WireKind.EngineData=>"Napęd",
        WireKind.PowerData=>"System energii",WireKind.WeaponData=>"Uzbrojenie",WireKind.ShieldData=>"Osłony",_=>kind.ToString()
    };
    public static Color WireColor(WireKind kind)=>kind switch
    {
        WireKind.Power=>Style.Amber,WireKind.Control=>Style.Cyan,WireKind.EngineData=>new("78adff"),
        WireKind.PowerData=>Style.Green,WireKind.WeaponData=>Style.Red,WireKind.ShieldData=>new("cd9eff"),_=>Style.Muted
    };
    public static string DataPorts(ShipModule module)
    {
        var labels=new List<string>();
        if(module.DataInputKind is { } input)labels.Add($"WE: {WireName(input)}");
        if(module.DataOutputKind is { } output)labels.Add($"WY: {WireName(output)}");
        return labels.Count==0?"Brak złączy danych":string.Join("  /  ",labels);
    }
    public static string State(ModuleState state)=>state switch
    {
        ModuleState.Active=>"PRACUJE",ModuleState.Idle=>"GOTOWY",ModuleState.NoControl=>"BRAK STEROWANIA",
        ModuleState.NoPower=>"BRAK ZASILANIA",ModuleState.Shed=>"ODŁĄCZONY",_=>"—"
    };
    public static Color StateColor(ModuleState state)=>state is ModuleState.Active or ModuleState.Idle ? Style.Green:state==ModuleState.NoControl?Style.Amber:Style.Red;
}
