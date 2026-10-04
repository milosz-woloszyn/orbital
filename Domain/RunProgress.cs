namespace Orbital.Domain;

public sealed record ShopOffer(ModuleKind Kind, int Price)
{
    public bool Sold { get; internal set; }
}
public sealed record StoredModule(int StockId, ModuleKind Kind, int Level=1, int Priority=5,
    WeaponMode Mode=WeaponMode.Unassigned, double Burst=0, double Gap=0, double? Energy=null, double? Shield=null);

/// <summary>One run's economy. Overflow XP is processed as separate, ordered level-up visits.</summary>
public sealed class RunProgress
{
    private readonly Random random;
    private readonly List<StoredModule> inventory=new();
    private int nextStock=1;
    public RunProgress(int seed) { random=new Random(seed); }
    public int Level { get; private set; }=1;
    public int Experience { get; private set; }
    public int Materials { get; private set; }
    public int Kills { get; private set; }
    public int NextLevelCost=>45+(Level-1)*18;
    public bool PanelOpen { get; private set; }
    public bool UpgradeAvailable { get; private set; }
    public bool Ended { get; private set; }
    public IReadOnlyList<StoredModule> Inventory=>inventory;
    public IReadOnlyList<ShopOffer> Offers { get; private set; }=Array.Empty<ShopOffer>();
    public void Reward(int experience,int materials)
    {
        if(experience<0 || materials<0)throw new ArgumentOutOfRangeException(nameof(experience));
        if(Ended)return;
        Experience=checked(Experience+experience);Materials=checked(Materials+materials);Kills++;
    }
    public static int Price(ModuleKind kind)=>kind switch
    {
        ModuleKind.Command or ModuleKind.EngineControl or ModuleKind.PowerControl or ModuleKind.WeaponControl or ModuleKind.ShieldControl=>20,
        ModuleKind.Battery=>30,ModuleKind.Engine=>45,ModuleKind.Generator=>55,ModuleKind.Blaster=>35,
        ModuleKind.Rocket=>45,ModuleKind.Emergency=>45,ModuleKind.Feedback=>40,ModuleKind.Dash=>45,
        ModuleKind.Shield=>60,ModuleKind.GuidedRocket=>65,ModuleKind.Laser=>75,ModuleKind.FanBlaster=>55,
        ModuleKind.Bomb=>55,ModuleKind.Seeker=>60,_=>40
    };
    public bool TryBeginLevel(ShipLayout layout)
    {
        if(Ended || PanelOpen || Experience<NextLevelCost)return false;
        Experience-=NextLevelCost;Level++;PanelOpen=true;UpgradeAvailable=true;
        if(Level%5==0)layout.Grow(2,2);
        Offers=Enumerable.Range(0,3).Select(_=>
        {
            var kind=ModuleCatalog.All[random.Next(ModuleCatalog.All.Count)].Kind;
            return new ShopOffer(kind,Price(kind));
        }).ToArray();
        return true;
    }
    public EditResult Buy(int index)
    {
        if(Ended || !PanelOpen || index<0 || index>=Offers.Count)return EditResult.Fail("Sklep otwiera się po awansie.");
        var offer=Offers[index];
        if(offer.Sold)return EditResult.Fail("Ten egzemplarz został już kupiony.");
        if(Materials<offer.Price)return EditResult.Fail("Za mało materiałów.");
        Materials-=offer.Price;offer.Sold=true;inventory.Add(new(nextStock++,offer.Kind));
        if(offer.Kind==ModuleKind.Shield)inventory.Add(new(nextStock++,ModuleKind.ShieldControl,Priority:9));
        return EditResult.Ok;
    }
    public EditResult Place(int stockId,ShipLayout layout,Cell cell,int rotation,out ShipModule? module,ShipSimulation? simulation=null)
    {
        module=null;
        var stock=inventory.FirstOrDefault(m=>m.StockId==stockId);
        if(Ended || !PanelOpen || stock==null)return EditResult.Fail("Brak modułu w magazynie.");
        var result=layout.TryAdd(stock.Kind,cell,out module,rotation);
        if(!result.Success)return result;
        module!.Level=stock.Level;module.Priority=stock.Priority;
        // Restoring inventory must not silently steal an occupied mouse channel.
        var mode=stock.Mode;int placedId=module.Id;
        if(mode is WeaponMode.Left or WeaponMode.Right && layout.Modules.Any(m=>m.Id!=placedId && m.WeaponMode==mode))mode=WeaponMode.Unassigned;
        if(module.Definition.IsWeapon)layout.ConfigureWeapon(module.Id,mode,stock.Burst,stock.Gap);
        if(simulation!=null)simulation.RestoreReserves(module.Id,stock.Energy,stock.Shield);
        inventory.Remove(stock);return EditResult.Ok;
    }
    public bool Store(ShipLayout layout,int id,ShipSimulation? simulation=null)
    {
        if(Ended || !PanelOpen || layout.Find(id) is not { } module)return false;
        var state=simulation?.Status.GetValueOrDefault(id);
        inventory.Add(new(nextStock++,module.Kind,module.Level,module.Priority,module.WeaponMode,module.BurstDuration,module.BurstGap,state?.StoredEnergy,state?.Shield));
        return layout.Remove(id);
    }
    public bool Upgrade(ShipLayout layout,int id)
    {
        if(Ended || !PanelOpen || !UpgradeAvailable || !layout.Upgrade(id))return false;
        UpgradeAvailable=false;return true;
    }
    public bool ClosePanel(bool forfeit=false)
    {
        if(Ended || !PanelOpen || UpgradeAvailable && !forfeit)return false;
        UpgradeAvailable=false;PanelOpen=false;return true;
    }
    public void End() { Ended=true;PanelOpen=false;UpgradeAvailable=false; }
}
