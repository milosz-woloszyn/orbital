using Godot;
using Orbital.Domain;

namespace Orbital;

public enum EditorTool { Select, Power, Control, Bridge, Erase }

/// <summary>Grid interaction and rendering. All edits are validated by ShipLayout.</summary>
public partial class ShipGrid : Control
{
    public float CellSize { get; set; }=48;
    public Func<ShipLayout> Layout { get; set; }=null!;
    public Func<int,ModuleStatus?> Status { get; set; }=null!;
    public event Action<string>? Edited;
    public event Action<int?>? SelectionChanged;
    public event Action<WireKind>? DataKindChanged;
    public WireKind DataKind { get; private set; }=WireKind.Control;
    public EditorTool Tool { get; private set; }
    public int? SelectedId { get; private set; }
    public ModuleKind? Pending { get; private set; }
    public int? MovingId { get; private set; }
    public bool HasPlacement=>Pending!=null || MovingId!=null;
    public int PlacementRotation { get; private set; }
    private Cell _hover=new(-1,-1);
    private Vector2 _pressPosition;
    private int? _dragId,_wireStartModule;
    private bool _drawing,_dragging;
    private readonly List<Cell> _path=new();
    private Cell _grabOffset;

    public override void _Ready()
    { MouseDefaultCursorShape=CursorShape.Cross; TextureFilter=TextureFilterEnum.Nearest; }
    public void SetTool(EditorTool tool)
    { Tool=tool; Cancel(); QueueRedraw(); }
    public void SetDataKind(WireKind kind)
    {
        if(kind==WireKind.Power)return;
        Cancel();DataKind=kind;Tool=EditorTool.Control;DataKindChanged?.Invoke(kind);QueueRedraw();
    }
    public void BeginAdd(ModuleKind kind)
    { Cancel(); Tool=EditorTool.Select; Pending=kind; PlacementRotation=0; Select(null); QueueRedraw(); }
    public void Select(int? id)
    { SelectedId=id; SelectionChanged?.Invoke(id); QueueRedraw(); }
    public void Cancel()
    { Pending=null; MovingId=null; _dragId=null; _drawing=false; _dragging=false; _path.Clear(); QueueRedraw(); }
    public void RotateSelection()
    {
        if(HasPlacement)
        {
            if(MovingId is int heldId && Layout().Find(heldId) is { } moving)
            {
                int height=PlacementRotation%2==0?moving.Definition.Height:moving.Definition.Width;
                _grabOffset=new(height-1-_grabOffset.Y,_grabOffset.X);
            }
            PlacementRotation=(PlacementRotation+1)%4; QueueRedraw(); return;
        }
        if(SelectedId is int id) Report(Layout().TryRotate(id),"Moduł obrócony. Sprawdź położenie portów.");
    }
    public void PickUpHovered()
    {
        if(!GetGlobalRect().HasPoint(GetGlobalMousePosition()) || HasPlacement)return;
        var cell=CellAt(GetLocalMousePosition());
        if(Layout().ModuleAt(cell) is not { } module)return;
        Cancel();Tool=EditorTool.Select;MovingId=module.Id;PlacementRotation=module.Rotation;
        _hover=cell;_grabOffset=new(cell.X-module.Position.X,cell.Y-module.Position.Y);Select(module.Id);
        Edited?.Invoke("Moduł w ręce: LPM umieszcza, R obraca, PPM / Esc przywraca poprzednie położenie.");
    }
    public void RemoveSelection()
    {
        if(HasPlacement) { Cancel();return; }
        if(SelectedId is int id) { Layout().Remove(id); Select(null); Edited?.Invoke("Moduł usunięty. Kable pozostają na siatce."); }
    }
    private Cell CellAt(Vector2 p)=>new((int)Math.Floor(p.X/CellSize),(int)Math.Floor(p.Y/CellSize));
    private WireKind Kind=>Tool==EditorTool.Power?WireKind.Power:DataKind;
    private Cell CablePoint(Cell cell)=>Layout().ModuleAt(cell) is { } m && m.HasPort(Kind) ? m.Port(Kind).CableCell : cell;

    public override void _GuiInput(InputEvent ev)
    {
        if(ev is InputEventMouseMotion motion)
        {
            _hover=CellAt(motion.Position);
            if(_dragId!=null && motion.Position.DistanceTo(_pressPosition)>7) _dragging=true;
            if(_drawing) ExtendPath(CablePoint(_hover));
            QueueRedraw();
        }
        if(ev is not InputEventMouseButton mouse) return;
        _hover=CellAt(mouse.Position);
        if(mouse.IsActionPressed("pickup")) { PickUpHovered();AcceptEvent();return; }
        if(mouse.ButtonIndex==MouseButton.Right && mouse.Pressed)
        {
            if(HasPlacement || _drawing || _dragging)Cancel();
            else RemoveAt(_hover);
            AcceptEvent();return;
        }
        if(mouse.ButtonIndex!=MouseButton.Left) return;
        AcceptEvent();
        if(mouse.Pressed)
        {
            if(!Layout().Contains(_hover)) return;
            if(MovingId is int movingId)
            {
                var result=Layout().TryMove(movingId,new(_hover.X-_grabOffset.X,_hover.Y-_grabOffset.Y),PlacementRotation);
                if(result.Success)Cancel();
                Report(result,"Moduł przeniesiony. Jego ustawienia i ulepszenia zostały zachowane.");return;
            }
            if(Tool is EditorTool.Power or EditorTool.Control)
            {
                var startModule=Layout().ModuleAt(_hover);
                if(Tool==EditorTool.Control && startModule!=null)
                {
                    var ports=Enum.GetValues<WireKind>().Where(k=>k!=WireKind.Power && startModule.HasPort(k)).ToArray();
                    if(ports.Length==0) { Edited?.Invoke("Ten moduł nie ma złącza danych.");return; }
                    DataKind=ports.MinBy(k=>PortPoint(startModule,k).DistanceSquaredTo(mouse.Position));
                    DataKindChanged?.Invoke(DataKind);
                }
                _path.Clear(); _path.Add(CablePoint(_hover)); _drawing=true;
                _wireStartModule=startModule?.Id; QueueRedraw(); return;
            }
            if(Tool==EditorTool.Bridge)
            { Report(Layout().TrySetBridge(_hover,!Layout().IsBridge(_hover)),"Mostek zmieniony."); return; }
            if(Tool==EditorTool.Erase) { RemoveAt(_hover);return; }
            if(Pending is { } kind)
            {
                string addedMessage="Moduł dodany. Podłącz port energii i port sterowania.";
                var result=Layout().TryAdd(kind,_hover,out var added,PlacementRotation);
                if(result.Success)
                {
                    Pending=null; Select(added!.Id);
                    // A shield comes with its controller, even in the free sandbox.
                    if(kind==ModuleKind.Shield && !Layout().Modules.Any(m=>m.Kind==ModuleKind.ShieldControl))
                    {
                        bool placed=false;
                        for(int y=1;y<Layout().Height-1 && !placed;y++) for(int x=1;x<Layout().Width && !placed;x++)
                        {
                            var cell=new Cell(x,y);
                            // Leave room for all three ports of the supplied controller.
                            if(new[] { Direction.North,Direction.West,Direction.South }.Any(d=>Layout().ModuleAt(cell+d.Offset())!=null))continue;
                            placed=Layout().TryAdd(ModuleKind.ShieldControl,cell,out _).Success;
                        }
                        if(!placed) addedMessage="Osłona dodana, ale brak miejsca na jej sterownik. Zwolnij pole i dodaj go z katalogu.";
                    }
                }
                Report(result,addedMessage); return;
            }
            var picked=Layout().ModuleAt(_hover); Select(picked?.Id);
            _dragId=picked?.Id; _pressPosition=mouse.Position;
            if(picked!=null) _grabOffset=new(_hover.X-picked.Position.X,_hover.Y-picked.Position.Y);
        }
        else
        {
            if(_drawing)
            {
                ExtendPath(CablePoint(_hover));
                var end=Layout().ModuleAt(_hover);
                var result=end!=null && !end.HasPort(Kind)
                    ? EditResult.Fail($"Niezgodna sieć danych: {Art.WireName(Kind)}. Wybierz pasujące złącze.")
                    : _wireStartModule is int start && end!=null && end.Id!=start
                        ? Layout().TryConnect(start,end.Id,Kind,true)
                        : Layout().TryDrawWire(Kind,_path);
                _drawing=false; _path.Clear(); Report(result,"Połączenie zapisane.");
            }
            if(_dragging && _dragId is int id)
                Report(Layout().TryMove(id,new(_hover.X-_grabOffset.X,_hover.Y-_grabOffset.Y)),"Moduł przeniesiony. Podłącz jego nowe porty.");
            _dragId=null; _dragging=false;
        }
        QueueRedraw();
    }
    public override void _Input(InputEvent ev)
    {
        if(IsVisibleInTree() && HasPlacement && ev is InputEventMouseButton { ButtonIndex:MouseButton.Right,Pressed:true })
        { Cancel();Edited?.Invoke("Anulowano przenoszenie.");GetViewport().SetInputAsHandled();return; }
        // Release outside the grid must never leave a sticky cable or dragging state.
        if(IsVisibleInTree() && ev is InputEventMouseButton { ButtonIndex:MouseButton.Left, Pressed:false } && !GetGlobalRect().HasPoint(GetGlobalMousePosition()))
        { _drawing=false;_dragId=null;_dragging=false;_path.Clear();QueueRedraw(); }
    }
    private void RemoveAt(Cell cell)
    {
        if(!Layout().Contains(cell))return;
        if(Layout().ModuleAt(cell) is { } module)
        { Layout().Remove(module.Id);if(SelectedId==module.Id)Select(null); }
        else
        {
            foreach(var kind in Enum.GetValues<WireKind>())Layout().RemoveWire(cell,kind);
            Layout().TrySetBridge(cell,false);
        }
        Edited?.Invoke("Element usunięty.");QueueRedraw();
    }
    private void ExtendPath(Cell end)
    {
        if(_path.Count==0 || !Layout().Contains(end)) return;
        var current=_path[^1];
        while(current!=end)
        {
            current=current.X!=end.X?new(current.X+Math.Sign(end.X-current.X),current.Y):new(current.X,current.Y+Math.Sign(end.Y-current.Y));
            if(_path.Count>1 && current==_path[^2]) _path.RemoveAt(_path.Count-1);
            else _path.Add(current);
        }
    }
    private void Report(EditResult result,string success)
    { Edited?.Invoke(result.Success?success:result.Error); QueueRedraw(); }

    public override void _Draw()
    {
        if(Layout==null) return;
        var layout=Layout();
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("0b1420"));
        for(int y=0;y<layout.Height;y++) for(int x=0;x<layout.Width;x++)
        {
            var rect=new Rect2(x*CellSize+1,y*CellSize+1,CellSize-2,CellSize-2);
            DrawRect(rect,new Color((x+y)%2==0?"121e2c":"101b29"));
            DrawCircle(rect.Position+new Vector2(3,3),.8f,new Color("334252"));
        }
        foreach(var wire in layout.Wires)
        {
            Vector2 center=new(wire.Cell.X*CellSize+CellSize/2,wire.Cell.Y*CellSize+CellSize/2);
            var color=Art.WireColor(wire.Kind);
            var offset=layout.IsBridge(wire.Cell)?Vector2.Zero:wire.Kind==WireKind.Power?new Vector2(-2,-2):new Vector2(2,2);
            foreach(var direction in Directions.All.Where(d=>wire.Connections.HasFlag(d)))
            {
                var delta=direction.Offset(); Vector2 end=center+new Vector2(delta.X,delta.Y)*CellSize/2;
                DrawLine(center+offset,end+offset,new Color("060b10"),8);
                DrawLine(center+offset,end+offset,color.Darkened(.15f),4);
                DrawLine(center+offset,end+offset,color.Lightened(.25f),1);
            }
            DrawCircle(center+offset,3,color);
        }
        foreach(var bridge in layout.Bridges)
        {
            var c=Center(bridge);
            var horizontal=layout.Wires.FirstOrDefault(w=>w.Cell==bridge && (w.Connections&(Direction.East|Direction.West))!=0);
            var color=horizontal!=null?Art.WireColor(horizontal.Kind):Style.Muted;
            DrawRect(new Rect2(c-new Vector2(10,6),new Vector2(20,12)),Style.Background);
            DrawArc(c,10,Mathf.Pi,Mathf.Tau,12,Style.Background,7);
            DrawArc(c,10,Mathf.Pi,Mathf.Tau,12,color,3);
        }
        foreach(var module in layout.Modules)
        {
            if(module.Id==MovingId)continue;
            Rect2 rect=new(module.Position.X*CellSize+4,module.Position.Y*CellSize+4,module.Width*CellSize-8,module.Height*CellSize-8);
            var status=Status?.Invoke(module.Id);
            var color=module.Id==SelectedId?Style.Cyan:status!=null?Art.StateColor(status.State).Darkened(.55f):Style.Border;
            DrawStyleBox(Style.Box(new Color("1b2c3b"),color,module.Id==SelectedId?2:1),rect);
            var inner=rect.Grow(-5);
            var icon=Art.Icon(module.Kind);
            bool turned=module.Rotation%2!=0;
            float scale=Math.Min(inner.Size.X/(turned?icon.GetHeight():icon.GetWidth()),inner.Size.Y/(turned?icon.GetWidth():icon.GetHeight()));
            var imageSize=icon.GetSize()*scale;
            DrawSetTransform(inner.GetCenter(),module.Rotation*Mathf.Pi/2);
            DrawTextureRect(icon,new Rect2(-imageSize/2,imageSize),false,new Color(1,1,1,.95f));
            DrawSetTransform(Vector2.Zero);
            // Controller badge distinguishes it from the shield emitter artwork.
            if(module.Definition.IsController)
            {
                DrawRect(new Rect2(rect.Position+new Vector2(3,3),new Vector2(18,15)),Style.Background);
                DrawString(Style.Mono,rect.Position+new Vector2(5,15),"C",HorizontalAlignment.Left,-1,12,Style.Cyan);
            }
            DrawCircle(rect.End-new Vector2(7,7),3,status!=null?Art.StateColor(status.State):Style.Muted);
            DrawString(Style.Mono,rect.Position+new Vector2(5,rect.Size.Y-5),module.Priority.ToString(),HorizontalAlignment.Left,-1,11,Style.Text);
            foreach(var portKind in Enum.GetValues<WireKind>().Where(module.HasPort))DrawPort(module,portKind);
        }
        if(_drawing)
            for(int i=0;i<_path.Count;i++)
            {
                var c=Center(_path[i]); DrawCircle(c,5,Art.WireColor(Kind));
                if(i>0) DrawLine(Center(_path[i-1]),c,Style.Text,2);
            }
        var candidate=Pending ?? (MovingId is int held?layout.Find(held)?.Kind:null) ?? (_dragging && _dragId is int moveId?layout.Find(moveId)?.Kind:null);
        if(candidate is { } kind)
        {
            var pos=_dragging || MovingId!=null?new Cell(_hover.X-_grabOffset.X,_hover.Y-_grabOffset.Y):_hover;
            int rotation=_dragging?layout.Find(_dragId!.Value)!.Rotation:PlacementRotation;
            var def=ModuleCatalog.Get(kind);
            var valid=layout.CanPlace(kind,pos,rotation,MovingId ?? (_dragging?_dragId:null)).Success;
            int w=rotation%2==0?def.Width:def.Height,h=rotation%2==0?def.Height:def.Width;
            var rect=new Rect2(pos.X*CellSize+2,pos.Y*CellSize+2,w*CellSize-4,h*CellSize-4);
            DrawRect(rect,new Color(valid?Style.Cyan:Style.Red,.18f));
            DrawRect(rect,valid?Style.Cyan:Style.Red,false,2);
            var icon=Art.Icon(kind);
            var size=icon.GetSize();if(rotation%2!=0)size=new(size.Y,size.X);
            float scale=Math.Min((rect.Size.X-8)/size.X,(rect.Size.Y-8)/size.Y);
            DrawSetTransform(rect.GetCenter(),rotation*Mathf.Pi/2);
            DrawTextureRect(icon,new Rect2(-icon.GetSize()*scale/2,icon.GetSize()*scale),false,new Color(1,1,1,.75f));
            DrawSetTransform(Vector2.Zero);
        }
        else if(layout.Contains(_hover))
            DrawRect(new Rect2(_hover.X*CellSize,_hover.Y*CellSize,CellSize,CellSize),new Color(Style.Cyan,.25f),false,1);
    }
    private Vector2 Center(Cell cell)=>new(cell.X*CellSize+CellSize/2,cell.Y*CellSize+CellSize/2);
    public override string _GetTooltip(Vector2 atPosition)
    {
        var module=Layout().ModuleAt(CellAt(atPosition));
        if(module==null)
        {
            var cell=CellAt(atPosition);
            var wires=Layout().Wires.Where(w=>w.Cell==cell).Select(w=>Art.WireName(w.Kind));
            return string.Join(" / ",wires)+(Layout().IsBridge(cell)?" — mostek, sieci rozdzielone":"");
        }
        var status=Status(module.Id);
        return $"{Art.Name(module.Kind)} · poziom {module.Level}\nPriorytet {module.Priority} · {module.Width} × {module.Height}\n{Art.DataPorts(module)}\n{status?.Reason}";
    }
    private Vector2 PortPoint(ShipModule module,WireKind kind)
    {
        var port=module.Port(kind);var dir=port.Facing.Offset();
        return Center(port.Cell)+new Vector2(dir.X,dir.Y)*(CellSize/2-2);
    }
    private void DrawPort(ShipModule module,WireKind kind)
    {
        var p=PortPoint(module,kind);
        DrawRect(new Rect2(p-new Vector2(5,5),new Vector2(10,10)),Style.Background);
        DrawRect(new Rect2(p-new Vector2(3,3),new Vector2(6,6)),Art.WireColor(kind));
    }
}
