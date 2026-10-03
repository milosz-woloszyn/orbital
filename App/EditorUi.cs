using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Main
{
    private Control _inspector=null!;
    private OptionButton _dataKindSelect=null!;
    private Label _editorShortcuts=null!;
    private Label _editorStats=null!,_toolHint=null!,_editorSelectionStatus=null!;
    private VBoxContainer _catalog=null!;
    private readonly Dictionary<EditorTool,Button> _toolButtons=new();
    private void BuildEditor()
    {
        _editorView=Screen();
        Header(_editorView,"ORBITAL  /  STOCZNIA","Zbuduj swój układ","Symulacja zatrzymana. Łącz systemy i sprawdzaj, jak zmienia się statek.");
        Style.Right(Style.Button(_editorView,"Ustawienia",1136,35,140,46,ShowSettings));
        Style.Right(Style.Button(_editorView,"Wróć do lotu  ↗",1288,35,152,46,()=>ShowView("arena"),true));
        var catalog=Style.StretchBottom(Style.Group(_editorView,0,116,250,762));
        Style.StretchBottom(Style.PanelAt(catalog,0,0,250,762));
        Style.Label(catalog,"KATALOG MODUŁÓW",18,13,218,24,13,Style.Cyan,true);
        var search=Style.Rect(new LineEdit { PlaceholderText="Szukaj modułu…",ClearButtonEnabled=true },16,52,218,38);
        catalog.AddChild(search);
        var scroll=Style.Rect(new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled },12,105,230,609);
        catalog.AddChild(scroll);Style.StretchBottom(scroll);
        _catalog=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill };
        _catalog.AddThemeConstantOverride("separation",6);scroll.AddChild(_catalog);
        search.TextChanged+=text=>PopulateCatalog(text);PopulateCatalog("");
        Style.Bottom(Style.Label(catalog,"∞  BEZ KOSZTÓW / BEZ LIMITU",18,729,218,20,10,Style.Muted,true));

        var center=Style.StretchBottom(Style.StretchRight(Style.Group(_editorView,268,116,860,762)));
        var toolNames=new[] { "1  Wybór", "2  Energia", "3  Dane", "4  Mostek", "5  Usuń" };
        for(int i=0;i<5;i++)
        {
            var tool=(EditorTool)i;
            _toolButtons[tool]=Style.Button(center,toolNames[i],i*113,0,105,43,()=>SetTool(tool));
        }
        Style.Right(Style.Button(center,"Połącz automatycznie",621,0,239,43,()=>
        {
            Grid.Cancel();
            var errors=Layout.AutoConnectAll();AfterEdit();
            Toast(errors.Count==0?"Wszystkie dostępne porty połączone. Sprawdź statusy modułów.":$"Nie połączono {errors.Count} tras. Zwolnij miejsce wokół portów.");
        }));
        _toolHint=Style.StretchRight(Style.Label(center,"",16,58,555,49,12,Style.Muted));
        _dataKindSelect=Style.Rect(new OptionButton(),621,60,239,36);center.AddChild(_dataKindSelect);Style.Right(_dataKindSelect);
        foreach(var kind in Enum.GetValues<WireKind>().Where(k=>k!=WireKind.Power))
            _dataKindSelect.AddItem("Dane: "+Art.WireName(kind),(int)kind);
        _dataKindSelect.TooltipText="Typ sieci podczas rysowania po pustych polach. Start na module wybiera najbliższe złącze danych.";
        var frame=Style.StretchBottom(Style.StretchRight(Style.PanelAt(center,0,110,860,552)));
        Grid=Style.Rect(new ShipGrid { Layout=()=>Layout, Status=id=>Simulation.Status.GetValueOrDefault(id) },0,0,768,480);
        frame.AddChild(Grid);
        var numbers=Enumerable.Range(1,Layout.Width).Select(x=>Style.Label(frame,x.ToString("00"),0,0,46,16,9,Style.Muted,true)).ToArray();
        foreach(var label in numbers)label.HorizontalAlignment=HorizontalAlignment.Center;
        void ResizeGrid()
        {
            Grid.CellSize=MathF.Floor(Math.Min((frame.Size.X-16)/Layout.Width,(frame.Size.Y-26)/Layout.Height));
            Grid.Size=new(Grid.CellSize*Layout.Width,Grid.CellSize*Layout.Height);
            Grid.Position=new((frame.Size.X-Grid.Size.X)/2,18+(frame.Size.Y-18-Grid.Size.Y)/2);
            for(int x=0;x<Layout.Width;x++) { numbers[x].Position=new(Grid.Position.X+x*Grid.CellSize,Grid.Position.Y-18);numbers[x].Size=new(Grid.CellSize,16); }
            Grid.QueueRedraw();
        }
        frame.Resized+=ResizeGrid;ResizeGrid();
        Grid.SelectionChanged+=_=>UpdateInspector();
        Grid.DataKindChanged+=kind=>
        {
            _dataKindSelect.Select(_dataKindSelect.GetItemIndex((int)kind));
            _dataKindSelect.AddThemeColorOverride("font_color",Art.WireColor(kind));
            HighlightTool(EditorTool.Control);UpdateToolHint();
        };
        _dataKindSelect.ItemSelected+=index=>Grid.SetDataKind((WireKind)_dataKindSelect.GetItemId((int)index));
        Grid.Edited+=message=>{ AfterEdit();Toast(message); };
        string[] legend=["PRĄD","DOWODZ.","NAPĘD","ENERGIA","BROŃ","OSŁONY"];
        for(int i=0;i<legend.Length;i++)
            Style.Bottom(Style.Label(center,"■ "+legend[i],16+i*137,665,130,20,10,Art.WireColor((WireKind)i),true));
        _editorShortcuts=Style.Bottom(Style.Right(Style.Label(center,"",334,690,520,20,10,Style.Muted,true)));
        Style.Bottom(Style.StretchRight(Style.PanelAt(center,0,715,860,47)));
        _editorStats=Style.Bottom(Style.StretchRight(Style.Label(center,"",17,726,828,25,12,Style.Muted,true)));

        _inspector=Style.Right(Style.StretchBottom(Style.PanelAt(_editorView,1146,116,294,762)));
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
        Grid?.SetTool(tool);HighlightTool(tool);UpdateToolHint();
    }
    private void UpdateToolHint()
    {
        _editorShortcuts.Text=$"{Controls.Name("pickup")}  PODNIEŚ   R  OBRÓT   PPM  USUŃ / ANULUJ";
        _toolHint.Text=Grid.Tool switch
        {
            EditorTool.Select=>"Kliknij moduł lub podnieś go skrótem. PPM usuwa obiekt.\nPodczas przenoszenia PPM / Esc anuluje.",
            EditorTool.Power=>"Prąd: przeciągnij od złącza do złącza lub rysuj po polach.\nSkrzyżowanie z inną siecią wymaga mostka.",
            EditorTool.Control=>"Dane: wybierz złącze lub typ sieci z listy obok.\nRóżne systemy krzyżuj mostkiem. Nie wolno ich łączyć.",
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
        _editorStats.Text=$"SIATKA {Layout.Width} × {Layout.Height}    MODUŁY {count:00}    GOTOWE {ready:00}    UWAGI {count-ready:00}    POJEMNOŚĆ {Simulation.Capacity:0} E";
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
        var reason=Style.Label(_inspector,status?.Reason??"",20,201,254,36,11,Style.Muted);reason.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        var ports=Style.Label(_inspector,Art.DataPorts(module),20,240,254,35,11,Style.Cyan);ports.AutowrapMode=TextServer.AutowrapMode.WordSmart;
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
