using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Main
{
    public bool IsLaboratory { get; private set; }=true;
    public RunProgress? Run { get; private set; }
    private bool _gameSmoke,_closeDialogOpen;
    private Control _menuView=null!,_labConsole=null!,_runConsole=null!;
    private Label _arenaTag=null!,_arenaTitle=null!,_runLevel=null!,_runCash=null!,_runStats=null!,_runSystems=null!,_deathSummary=null!;
    private Label _catalogTitle=null!,_catalogFooter=null!;
    private Button _shipButton=null!,_autoConnectButton=null!;
    private Meter _xpBar=null!;
    private ScrollContainer _gridScroll=null!;
    private Action? ResizeShipGrid;
    private int _placingStock;
    private string _catalogFilter="";

    private void BuildMenu()
    {
        _menuView=Screen();
        var backdrop=new MenuBackdrop();_menuView.AddChild(backdrop);backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Style.Label(_menuView,"LOT • WALKA • KONSTRUKCJA",96,160,1000,28,14,Style.Cyan,true);
        Style.Label(_menuView,"ORBITAL",88,200,1100,115,92);
        Style.Label(_menuView,"Przetrwaj ostrzał. Zbuduj statek, który wytrzyma więcej.",96,330,900,40,23,Style.Muted);
        Style.Label(_menuView,"Każdy awans to nowy układ możliwości.\nPo śmierci zaczynasz od nowa.",96,389,730,66,19,Style.Muted);
        Style.Button(_menuView,"Nowa rozgrywka  ↗",96,510,430,60,()=>StartRun(),true);
        Style.Button(_menuView,"Laboratorium / wersja testowa",96,586,430,52,StartLaboratory);
        Style.Button(_menuView,"Ustawienia",96,654,208,46,ShowSettings);
        Style.Button(_menuView,"Wyjdź",318,654,208,46,()=>QuitCleanly());
        Style.Label(_menuView,$"{ModuleCatalog.All.Count} MODUŁÓW   /   7 BRONI   /   5 TYPÓW WROGÓW",96,752,970,26,13,Style.Cyan,true);
        Style.Label(_menuView,"WASD — lot    Mysz — cel    LPM / PPM — ogień    Esc — pauza",96,800,1050,26,13,Style.Muted,true);
    }
    private void BuildRunHud()
    {
        _runConsole=Style.Right(Style.StretchBottom(Style.Group(_arenaView,1136,96,304,630)));
        Style.StretchBottom(Style.PanelAt(_runConsole,0,0,304,630));
        Style.Label(_runConsole,"TRANSMISJA / NA ŻYWO",20,20,264,26,12,Style.Cyan,true);
        _runLevel=Style.Label(_runConsole,"POZIOM 01",20,66,264,36,27);
        _runCash=Style.Label(_runConsole,"0 M",20,121,264,42,32,Style.Amber);
        Style.Label(_runConsole,"MATERIAŁY",20,163,264,22,11,Style.Muted,true);
        _runStats=Style.Label(_runConsole,"",20,221,264,110,17,Style.Muted,true);
        _runSystems=Style.Label(_runConsole,"",20,357,264,65,13,Style.Green,true);
        Style.Label(_runConsole,"Awans zatrzymuje walkę.\nKup moduły i wykorzystaj jedno\ndarmowe ulepszenie.",20,448,264,80,15,Style.Muted);
        Style.Label(_runConsole,"Punkt na środku statku\nwyznacza obszar trafienia.\nUnikaj czerwonych stref rakiet.",20,548,264,67,13,Style.Muted);
        _xpBar=Style.Rect(new Meter { Tint=Style.Cyan },24,86,1068,5);_arenaView.AddChild(_xpBar);Style.StretchRight(_xpBar);
        _runConsole.Visible=false;_xpBar.Visible=false;
    }
    public void StartRun(int? seed=null)
    {
        int value=seed??System.Environment.TickCount;IsLaboratory=false;Run=new RunProgress(value);
        ResetShip(false);Flight.SetCombat(true,value);Flight.AimOverride=Vector2.Zero;
        Flight.World.Rewarded+=reward=>
        {
            if(!Simulation.IsDestroyed) { Run.Reward(reward.Experience,reward.Materials);Flight.RewardFeedback(reward); }
        };
        Flight.World.PlayerDamaged+=damage=> { if(!Simulation.IsDestroyed) { Simulation.Damage(damage);Flight.Hit(); } };
        RefreshMode();ShowView("arena");Toast("Przetrwaj. Zabijaj, zdobywaj materiały, rozwijaj statek.");
    }
    private void StartLaboratory()
    {
        IsLaboratory=true;Run=null;ResetShip(false);Flight.SetCombat(false,1);Flight.AimOverride=Vector2.Zero;
        RefreshMode();ShowView("arena");Toast("Laboratorium: wszystkie moduły i ulepszenia bez kosztów.");
    }
    private void RefreshMode()
    {
        _labConsole.Visible=IsLaboratory;_runConsole.Visible=!IsLaboratory;_xpBar.Visible=!IsLaboratory;
        _arenaTag.Text=IsLaboratory?"ORBITAL  /  LABORATORIUM 01":"ORBITAL  /  PRZETRWANIE";
        _arenaTitle.Text=IsLaboratory?"Swobodny lot":"Nie zatrzymuj się.";
        _shipButton.Disabled=!IsLaboratory;_shipButton.Text=IsLaboratory?"Statek  ↗":"Po awansie";
        _autoConnectButton.Visible=IsLaboratory;
        _catalogTitle.Text=IsLaboratory?"KATALOG MODUŁÓW":"SKLEP / MAGAZYN";
        _catalogFooter.Text=IsLaboratory?"∞  BEZ KOSZTÓW / BEZ LIMITU":"PPM → MODUŁ DO MAGAZYNU";
        PopulateCatalog("");UpdateInspector();RebuildDeathPanel();ResizeShipGrid?.Invoke();
    }
    private void RebuildDeathPanel()
    {
        foreach(var child in _deadPanel.GetChildren()) { _deadPanel.RemoveChild(child);child.QueueFree(); }
        _deadPanel.Size=new(470,245);_deadPanel.Position=Flight.Bounds.GetCenter()-_deadPanel.Size/2;
        Style.Label(_deadPanel,IsLaboratory?"KADŁUB ZNISZCZONY":"KONIEC TRANSMISJI",24,20,430,36,26,Style.Red);
        _deathSummary=Style.Label(_deadPanel,"",24,69,422,80,15,Style.Muted);_deathSummary.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        Style.Button(_deadPanel,IsLaboratory?"Napraw i wróć do lotu":"Jeszcze jeden lot  ↗",24,155,422,43,()=>
        { if(IsLaboratory)Simulation.Repair();else StartRun(); },true);
        Style.Button(_deadPanel,"Menu główne",24,205,422,32,()=>ReturnToMenu(false));
    }
    private void UpdateRun()
    {
        if(IsLaboratory || Run==null)return;
        if(Simulation.IsDestroyed) { Run.End();return; }
        if(Run.TryBeginLevel(Layout))OpenLevelPanel();
    }
    private void OpenLevelPanel()
    {
        Grid.Cancel();Grid.Select(null);_placingStock=0;
        Flight.World.ClearHostileBullets();_audio.Play("level");
        PopulateCatalog("");ResizeShipGrid?.Invoke();ShowView("editor");
        Toast($"POZIOM {Run!.Level} — sklep odświeżony. Wybierz jedno darmowe ulepszenie."+(Run.Level%5==0?" Siatka +2 × +2.":""));
    }
    private void RequestReturnToFlight()
    {
        if(IsLaboratory) { ShowView("arena");return; }
        if(Run==null || !Run.PanelOpen || _closeDialogOpen)return;
        if(Run.UpgradeAvailable)
        {
            _closeDialogOpen=true;
            var dialog=new ConfirmationDialog { Title="Pominąć darmowe ulepszenie?",DialogText="Nie wykorzystano ulepszenia z tego awansu.\nPo powrocie do walki ta możliwość przepadnie.",OkButtonText="Pomiń i leć",CancelButtonText="Wróć do ulepszenia" };
            AddChild(dialog);
            dialog.Confirmed+=()=>{ _closeDialogOpen=false;dialog.QueueFree();FinishLevel(true); };
            dialog.Canceled+=()=>{ _closeDialogOpen=false;dialog.QueueFree(); };dialog.PopupCentered(new(510,180));return;
        }
        FinishLevel(false);
    }
    private void FinishLevel(bool forfeit)
    {
        if(!Run!.ClosePanel(forfeit))return;
        Grid.Cancel();Simulation.Refresh();
        if(Run.TryBeginLevel(Layout))OpenLevelPanel();else ShowView("arena");
    }
    private void UpdateRunHud()
    {
        if(_runLevel==null)return;
        if(!IsLaboratory && Run!=null)
        {
            _runLevel.Text=$"POZIOM {Run.Level:00}";_runCash.Text=$"{Run.Materials} M";
            _runStats.Text=$"CZAS  {TimeSpan.FromSeconds(Flight.World.Time):mm\\:ss}\nZABÓJSTWA  {Run.Kills}\nEXP  {Run.Experience} / {Run.NextLevelCost}\nNAPÓR  ×{Flight.World.Threat:0.0}";
            int ready=Simulation.Status.Values.Count(s=>s.Powered);
            _runSystems.Text=$"SYSTEMY {ready:00} / {Layout.Modules.Count:00}\n"+(ready==Layout.Modules.Count?"ZASILANIE I DANE  OK":"SPRAWDŹ UKŁAD PO AWANSIE");
            _runSystems.AddThemeColorOverride("font_color",ready==Layout.Modules.Count?Style.Green:Style.Amber);
            _xpBar.Value=Run.Experience;_xpBar.Maximum=Run.NextLevelCost;_xpBar.QueueRedraw();
        }
        if(_deathSummary!=null)
            _deathSummary.Text=IsLaboratory?"To laboratorium. Napraw statek\nlub przywróć zestaw początkowy.":
                $"Czas lotu: {TimeSpan.FromSeconds(Flight.World.Time):mm\\:ss}   •   Poziom {Run!.Level}\nPokonani przeciwnicy: {Run.Kills}\nNowy lot zaczyna się bez zachowanego postępu.";
    }
    private void ReturnToMenu(bool confirm=true)
    {
        if(confirm && !IsLaboratory && Run is { Ended:false })
        {
            var dialog=new ConfirmationDialog { Title="Zakończyć ten lot?",DialogText="Bieżący statek i postęp tego lotu zostaną utracone.",OkButtonText="Zakończ lot",CancelButtonText="Kontynuuj" };
            AddChild(dialog);dialog.Confirmed+=()=>{dialog.QueueFree();ReturnToMenu(false);};dialog.Canceled+=()=>dialog.QueueFree();dialog.PopupCentered(new(470,160));return;
        }
        Run?.End();_capture=null;Grid.Cancel();ShowView("menu");
    }
    private void PopulateRunCatalog(string filter)
    {
        foreach(var child in _catalog.GetChildren()) { _catalog.RemoveChild(child);child.QueueFree(); }
        if(Run==null)return;
        void Text(string text,Color color)
        {
            var label=new Label { Text=text,CustomMinimumSize=new(210,29),AutowrapMode=TextServer.AutowrapMode.WordSmart };
            label.AddThemeFontSizeOverride("font_size",13);label.AddThemeColorOverride("font_color",color);_catalog.AddChild(label);
        }
        Text($"POZIOM {Run.Level}   /   {Run.Materials} M",Style.Amber);
        Text(Run.UpgradeAvailable?"DARMOWE ULEPSZENIE: 1\nWybierz zamontowany moduł.":"ULEPSZENIE WYKORZYSTANE",Run.UpgradeAvailable?Style.Green:Style.Muted);
        for(int i=0;i<Run.Offers.Count;i++)
        {
            int index=i;var offer=Run.Offers[i];
            var button=new Button { Text=$"{Art.Name(offer.Kind)}\n{(offer.Sold?"KUPIONO":offer.Price+" M")}",Icon=Art.Icon(offer.Kind),ExpandIcon=true,
                CustomMinimumSize=new(215,68),Disabled=offer.Sold || Run.Materials<offer.Price,TooltipText=Art.Description(offer.Kind)+(offer.Kind==ModuleKind.Shield?"\nSterownik osłon w zestawie.":"") };
            button.AddThemeConstantOverride("icon_max_width",32);button.AddThemeFontSizeOverride("font_size",13);
            button.Pressed+=()=>{ var result=Run.Buy(index);Toast(result.Success?"Kupiono moduł. Wybierz go z magazynu poniżej.":result.Error);PopulateCatalog(filter);_audio.Play("click"); };
            _catalog.AddChild(button);
        }
        Text("MAGAZYN / DO ZAMONTOWANIA",Style.Cyan);
        foreach(var stock in Run.Inventory.Where(s=>Art.Name(s.Kind).Contains(filter,StringComparison.OrdinalIgnoreCase)))
        {
            var button=new Button { Text=$"{Art.Name(stock.Kind)}  ·  {stock.Level}",Icon=Art.Icon(stock.Kind),ExpandIcon=true,CustomMinimumSize=new(215,48),TooltipText=Art.Description(stock.Kind) };
            button.AddThemeConstantOverride("icon_max_width",28);button.AddThemeFontSizeOverride("font_size",12);
            button.Pressed+=()=>{ _placingStock=stock.StockId;Grid.BeginAdd(stock.Kind);HighlightTool(EditorTool.Select);Toast("Wskaż wolne pole. R obraca, PPM anuluje."); };
            _catalog.AddChild(button);
        }
        if(Run.Inventory.Count==0)Text("Magazyn pusty. Kup moduł lub\nzdejmij go z siatki przez PPM.",Style.Muted);
    }
    private void UpdateGridScroll(float dt)
    {
        if(_view!="editor" || _gridScroll==null)return;
        var p=_gridScroll.GetLocalMousePosition();var size=_gridScroll.Size;
        if(p.X<0 || p.Y<0 || p.X>size.X || p.Y>size.Y)return;
        const float edge=26,speed=420;
        int x=p.X<edge?-1:p.X>size.X-edge?1:0,y=p.Y<edge?-1:p.Y>size.Y-edge?1:0;
        _gridScroll.ScrollHorizontal+=(int)(x*speed*dt);_gridScroll.ScrollVertical+=(int)(y*speed*dt);
    }
}

public partial class MenuBackdrop : Control
{
    public override void _Ready() { MouseFilter=MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),Style.Background);
        var random=new Random(71);
        for(int i=0;i<220;i++)DrawCircle(new((float)random.NextDouble()*Size.X,(float)random.NextDouble()*Size.Y),i%4==0?1.3f:.7f,new Color(.5f,.75f,.8f,.3f));
        var center=new Vector2(Size.X*.8f,Size.Y*.47f);
        for(int i=0;i<5;i++)DrawArc(center,120+i*75,0,Mathf.Tau,120,new Color(Style.Cyan,.035f+i*.008f),1);
        DrawSetTransform(center,-.32f);DrawTextureRect(Art.Player,new Rect2(-130,-145,260,290),false);DrawSetTransform(Vector2.Zero);
    }
}
