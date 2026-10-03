using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Main
{
    private Control _inspector=null!;
    private Label _editorStats=null!,_toolHint=null!,_editorSelectionStatus=null!;
    private VBoxContainer _catalog=null!;
    private readonly Dictionary<EditorTool,Button> _toolButtons=new();
    private void BuildEditor()
    {
        _editorView=Screen();
        Header(_editorView,"ORBITAL  /  STOCZNIA","Zbuduj swój układ","Symulacja zatrzymana. Łącz systemy i sprawdzaj, jak zmienia się statek.");
        Style.Button(_editorView,"Ustawienia",1112,35,140,46,ShowSettings);
        Style.Button(_editorView,"Wróć do lotu  ↗",1264,35,152,46,()=>ShowView("arena"),true);
        Style.PanelAt(_editorView,24,136,250,718);
        Style.Label(_editorView,"KATALOG MODUŁÓW",42,149,218,24,13,Style.Cyan,true);
        var search=Style.Rect(new LineEdit { PlaceholderText="Szukaj modułu…",ClearButtonEnabled=true },40,188,218,38);
        _editorView.AddChild(search);
        var scroll=Style.Rect(new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled },36,241,230,565);
        _editorView.AddChild(scroll);
        _catalog=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill };
        _catalog.AddThemeConstantOverride("separation",6);scroll.AddChild(_catalog);
        search.TextChanged+=text=>PopulateCatalog(text);PopulateCatalog("");
        Style.Label(_editorView,"∞  BEZ KOSZTÓW / BEZ LIMITU",42,821,218,20,10,Style.Muted,true);

        var toolNames=new[] { "1  Wybór", "2  Energia", "3  Dane", "4  Mostek", "5  Usuń" };
        for(int i=0;i<5;i++)
        {
            var tool=(EditorTool)i;
            _toolButtons[tool]=Style.Button(_editorView,toolNames[i],292+i*113,136,105,43,()=>SetTool(tool));
        }
        Style.Button(_editorView,"Połącz automatycznie",865,136,239,43,()=>
        {
            var errors=Layout.AutoConnectAll();AfterEdit();
            Toast(errors.Count==0?"Wszystkie dostępne porty połączone. Sprawdź statusy modułów.":$"Nie połączono {errors.Count} tras. Zwolnij miejsce wokół portów.");
        });
        _toolHint=Style.Label(_editorView,"",310,194,788,49,13,Style.Muted);
        Style.PanelAt(_editorView,292,246,812,516);
        Grid=Style.Rect(new ShipGrid { Layout=()=>Layout, Status=id=>Simulation.Status.GetValueOrDefault(id) },314,264,768,480);
        _editorView.AddChild(Grid);
        Grid.SelectionChanged+=_=>UpdateInspector();
        Grid.Edited+=message=>{ AfterEdit();Toast(message); };
        for(int x=0;x<16;x++)Style.Label(_editorView,(x+1).ToString("00"),314+x*48,246,46,16,9,Style.Muted,true).HorizontalAlignment=HorizontalAlignment.Center;
        Style.Label(_editorView,"■  ENERGIA",308,774,150,24,11,Style.Amber,true);
        Style.Label(_editorView,"■  STEROWANIE",456,774,185,24,11,Style.Cyan,true);
        Style.Label(_editorView,"R  OBRÓT     DEL  USUŃ     PPM  ANULUJ",681,774,407,24,11,Style.Muted,true);
        Style.PanelAt(_editorView,292,807,812,47);
        _editorStats=Style.Label(_editorView,"",309,818,780,25,12,Style.Muted,true);

        _inspector=Style.PanelAt(_editorView,1122,136,294,718);
        SetTool(EditorTool.Select);UpdateInspector();
    }
    private void PopulateCatalog(string filter)
    {
        foreach(var child in _catalog.GetChildren()) { _catalog.RemoveChild(child);child.QueueFree(); }
        var groups=new[]
        {
            ("01  ZASILANIE",new[] { ModuleKind.Generator,ModuleKind.Battery,ModuleKind.Emergency,ModuleKind.Feedback }),
            ("02  DOWODZENIE",new[] { ModuleKind.Command,ModuleKind.PowerControl,ModuleKind.EngineControl,ModuleKind.WeaponControl,ModuleKind.ShieldControl }),
            ("03  NAPĘD I UZBROJENIE",new[] { ModuleKind.Engine,ModuleKind.Dash,ModuleKind.Blaster,ModuleKind.Rocket,ModuleKind.Shield })
        };
        foreach(var (title,kinds) in groups)
        {
            var shown=kinds.Where(k=>Art.Name(k).Contains(filter,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(shown.Length==0)continue;
            var label=new Label { Text=title,CustomMinimumSize=new(0,30) };
            label.AddThemeFontOverride("font",Style.Mono);label.AddThemeFontSizeOverride("font_size",10);label.AddThemeColorOverride("font_color",Style.Muted);_catalog.AddChild(label);
            foreach(var kind in shown)
            {
                var d=ModuleCatalog.Get(kind);
                var button=new Button
                {
                    Text=Art.Name(kind),Icon=Art.Icon(kind),ExpandIcon=true,
                    Alignment=HorizontalAlignment.Left,CustomMinimumSize=new(215,48),
                    TooltipText=$"{Art.Name(kind)} · {d.Width} × {d.Height}\n{Art.Description(kind)}\nKliknij, następnie wskaż miejsce na siatce.",
                    MouseDefaultCursorShape=CursorShape.PointingHand
                };
                button.AddThemeConstantOverride("icon_max_width",32);button.AddThemeFontSizeOverride("font_size",13);
                button.Pressed+=()=>{ Grid.BeginAdd(kind);HighlightTool(EditorTool.Select);_toolHint.Text=$"Umieść: {Art.Name(kind)}  •  R obraca  •  PPM anuluje\nZajmowane pola muszą być wolne od kabli i innych modułów.";_audio.Play("click"); };
                _catalog.AddChild(button);
            }
        }
    }
    private void SetTool(EditorTool tool)
    {
        Grid?.SetTool(tool);HighlightTool(tool);
        _toolHint.Text=tool switch
        {
            EditorTool.Select=>"Kliknij moduł, aby go skonfigurować. Przeciągnij, aby zmienić położenie.\nNowe podzespoły wybierzesz z katalogu po lewej.",
            EditorTool.Power=>"Energia: przeciągnij od modułu do modułu, aby wyznaczyć trasę.\nNa pustych polach rysuj ręcznie; skrzyżowania tego samego typu łączą sieci.",
            EditorTool.Control=>"Sterowanie: połącz dowodzenie ze sterownikami, a sterowniki z podzespołami.\nPrzeciągnięcie między modułami wyznacza trasę. Sygnał nie przechodzi przez moduły.",
            EditorTool.Bridge=>"Kliknij pole, aby dodać lub usunąć mostek.\nNa mostku trasy pionowa i pozioma przechodzą bez połączenia.",
            _=>"Kliknij, aby usunąć moduł albo kable i mostek z pola.\nUsunięcie modułu nie usuwa pozostałych kabli."
        };
    }
    private void HighlightTool(EditorTool tool)
    {
        foreach(var (key,button) in _toolButtons)
            button.AddThemeStyleboxOverride("normal",Style.Box(key==tool?new Color("23404b"):Style.Panel,key==tool?Style.Cyan:Style.Border));
    }
    private void UpdateEditorStats()
    {
        if(_editorStats==null)return;
        int ready=Simulation.Status.Values.Count(s=>s.Powered),count=Layout.Modules.Count;
        _editorStats.Text=$"SIATKA 16 × 10    MODUŁY {count:00}    GOTOWE {ready:00}    UWAGI {count-ready:00}    POJEMNOŚĆ {Simulation.Capacity:0} E";
        if(Grid.SelectedId is int id && _editorSelectionStatus!=null && Simulation.Status.TryGetValue(id,out var status))
        { _editorSelectionStatus.Text=Art.State(status.State);_editorSelectionStatus.AddThemeColorOverride("font_color",Art.StateColor(status.State)); }
    }
    private void UpdateInspector()
    {
        if(_inspector==null || Grid==null)return;
        foreach(var child in _inspector.GetChildren()) { _inspector.RemoveChild(child);child.QueueFree(); }
        _editorSelectionStatus=null!;
        if(Grid.SelectedId is not int id || Layout.Find(id) is not { } module)
        {
            Style.Label(_inspector,"INSPEKTOR",20,18,254,30,12,Style.Cyan,true);
            Style.Label(_inspector,"Każdy moduł\nma znaczenie.",20,87,254,90,27);
            Style.Label(_inspector,"Wybierz podzespół na siatce,\naby zobaczyć jego połączenia,\npriorytet i parametry.",20,202,254,86,15,Style.Muted);
            Style.Label(_inspector,"01  DODAJ MODUŁ\n02  PODŁĄCZ ENERGIĘ\n03  PODŁĄCZ STEROWANIE\n04  WRÓĆ DO LOTU",20,356,254,136,13,Style.Muted,true);
            Style.Button(_inspector,"Usuń wszystkie kable",20,578,254,42,()=>ConfirmClearWires());
            Style.Label(_inspector,"Gotowy układ możesz przywrócić\nw konsoli testowej na arenie.",20,645,254,48,13,Style.Muted);
            return;
        }
        var image=Style.Rect(new TextureRect { Texture=Art.Icon(module.Kind),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore },18,19,68,66);
        _inspector.AddChild(image);
        var title=Style.Label(_inspector,Art.Name(module.Kind),98,15,177,64,21);title.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        var status=Simulation.Status.GetValueOrDefault(id);
        _editorSelectionStatus=Style.Label(_inspector,status!=null?Art.State(status.State):"—",20,102,254,25,12,status!=null?Art.StateColor(status.State):Style.Muted,true);
        Style.Label(_inspector,Art.Description(module.Kind),20,141,254,58,14,Style.Muted);
        var reason=Style.Label(_inspector,status?.Reason??"",20,207,254,53,12,Style.Muted);reason.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        Style.Label(_inspector,"Priorytet energii",20,283,163,27,15);
        var priority=Spin(_inspector,190,277,84,40,1,10,1,module.Priority);
        priority.ValueChanged+=value=>{ module.Priority=(int)value;Simulation.Refresh();UpdateEditorStats();Grid.QueueRedraw(); };
        Style.Label(_inspector,$"Poziom {module.Level}",20,332,130,29,16);
        Style.Button(_inspector,"Ulepsz  +1",152,326,122,39,()=>{ Layout.Upgrade(id);AfterEdit();Toast("Moduł ulepszony bez kosztów."); });
        Style.Button(_inspector,"Obróć  R",20,384,122,40,()=>{ Grid.RotateSelection();AfterEdit(); });
        Style.Button(_inspector,"Usuń",152,384,122,40,()=>{ Grid.RemoveSelection();AfterEdit(); });
        string distance=status is { SourceDistance:<int.MaxValue }?$"{status.SourceDistance} pól kabla":"brak trasy";
        string summary=$"WYMIARY {module.Width} × {module.Height}    ID {id:00}\nŹRÓDŁO: {distance}";
        if(module.Definition.IsWeapon) summary+=$"\n{module.Definition.Damage*module.UpgradeFactor:0} DMG / {module.Definition.ShotInterval:0.##}s / {module.Definition.ShotEnergy:0.#} E";
        Style.Label(_inspector,summary,20,440,254,57,11,Style.Muted,true);
        if(module.Definition.IsWeapon)
        {
            Style.Label(_inspector,"Przypisanie broni",20,506,254,25,14);
            var mode=Style.Rect(new OptionButton(),20,537,254,36);
            mode.AddItem("Aktywna / kanał LPM",(int)WeaponMode.Left);mode.AddItem("Aktywna / kanał PPM",(int)WeaponMode.Right);
            mode.AddItem("Pasywna",(int)WeaponMode.Passive);mode.AddItem("Nieprzypisana",(int)WeaponMode.Unassigned);
            mode.Select((int)module.WeaponMode);_inspector.AddChild(mode);
            mode.ItemSelected+=index=>{ Layout.ConfigureWeapon(id,(WeaponMode)index,module.BurstDuration,module.BurstGap);AfterEdit(); };
            if(module.WeaponMode==WeaponMode.Passive)
            {
                Style.Label(_inspector,"Seria [s]",20,586,117,24,13);Style.Label(_inspector,"Przerwa [s]",154,586,120,24,13);
                var burst=Spin(_inspector,20,616,120,36,0,999999,.1,module.BurstDuration,true);
                var gap=Spin(_inspector,154,616,120,36,0,999999,.1,module.BurstGap,true);
                burst.ValueChanged+=value=>{ Layout.ConfigureWeapon(id,module.WeaponMode,value,module.BurstGap);Simulation.Refresh(); };
                gap.ValueChanged+=value=>{ Layout.ConfigureWeapon(id,module.WeaponMode,module.BurstDuration,value);Simulation.Refresh(); };
                Style.Label(_inspector,"Seria 0 = ciągły ostrzał",20,673,254,22,12,Style.Amber);
            }
        }
        else
        {
            var d=module.Definition;double scale=module.UpgradeFactor;
            string stats=d.IsEnergy?(module.Kind==ModuleKind.Battery?$"POJEMNOŚĆ  {d.Capacity*scale:0} E":module.Kind==ModuleKind.Feedback?$"ODZYSK  {d.FeedbackEnergy*scale:0} E / trafienie":$"PRODUKCJA  {d.Generation*scale:0} E/s")
                :module.Kind==ModuleKind.Shield?$"OSŁONA  {d.ShieldCapacity*scale:0}\nŁADOWANIE  {d.ShieldRecharge*scale:0}/s":$"POBÓR  {d.Consumption:0.#} E/s";
            Style.Label(_inspector,stats,20,530,254,70,13,Style.Cyan,true);
            if(module.Kind==ModuleKind.Emergency)Style.Label(_inspector,$"Impuls {d.Duration:0}s / odnowienie {d.Cooldown:0}s",20,608,254,30,13,Style.Muted);
        }
    }
    private static SpinBox Spin(Control parent,float x,float y,float w,float h,double min,double max,double step,double value,bool greater=false)
    {
        var spin=Style.Rect(new SpinBox { MinValue=min,MaxValue=max,Step=step,Value=value,AllowGreater=greater },x,y,w,h);
        parent.AddChild(spin);return spin;
    }
    private void ConfirmClearWires()
    {
        var dialog=new ConfirmationDialog { Title="Usunąć wszystkie kable?",DialogText="Moduły pozostaną na swoich miejscach.\nUsunięte zostaną oba rodzaje kabli i mostki.",OkButtonText="Usuń kable",CancelButtonText="Anuluj" };
        AddChild(dialog);dialog.Confirmed+=()=>{ Layout.ClearWires();AfterEdit();Toast("Kable usunięte."); };
        dialog.Canceled+=()=>dialog.QueueFree();dialog.Confirmed+=()=>dialog.QueueFree();dialog.PopupCentered(new(470,170));
    }
}
