using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Arena : Control
{
    public Rect2 Bounds=>new(0,96,Math.Max(1,Size.X-324),Math.Max(1,Size.Y-270));
    public Func<float,FrameIntent,SimulationFrame> Tick { get; set; }=null!;
    public Func<bool> Paused { get; set; }=()=>false;
    public Func<bool> Dead { get; set; }=()=>false;
    public Func<float> Shield { get; set; }=()=>0;
    public Sound Audio { get; set; }=null!;
    public Vector2 ShipPosition { get; private set; }=new(558,530);
    public Vector2 Velocity { get; private set; }
    public int ProjectileCount=>_projectiles.Count;
    public int ShotsFired { get; private set; }
    public int ExplosionsCreated { get; private set; }
    public Vector2 AimOverride { get; set; }=Vector2.Zero;
    private Vector2 _dashDirection;
    private float _angle,_clock,_flash,_shake;
    private bool _dashVisual;
    private readonly List<Projectile> _projectiles=new();
    private readonly List<Spark> _sparks=new();
    private readonly List<Blast> _blasts=new();
    private readonly Random _rng=new(328);
    private readonly List<(Vector2 Pos,float Size,float Phase)> _stars=new();
    private sealed class Projectile
    { public Vector2 Position,Direction,Target; public float Speed,Radius,Age; public bool Rocket; }
    private sealed class Spark
    { public Vector2 Position,Velocity; public float Life,MaxLife; public Color Color; }
    private sealed class Blast { public Vector2 Position; public float Life,Radius; }
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore; TextureFilter=TextureFilterEnum.Nearest;
        for(int i=0;i<260;i++) _stars.Add((new Vector2((float)_rng.NextDouble(),(float)_rng.NextDouble()),(float)_rng.NextDouble()*1.3f+.4f,(float)_rng.NextDouble()*6));
    }
    public void Reset()
    { ShipPosition=new(Bounds.GetCenter().X,Bounds.End.Y-156);Velocity=Vector2.Zero;_projectiles.Clear();_sparks.Clear();_blasts.Clear();_dashVisual=false; }
    public void Hit()
    { _flash=.3f;_shake=5; Emit(ShipPosition,Style.Red,24,160);Audio.Play("hit"); }
    public override void _PhysicsProcess(double delta)
    {
        if(Paused() || Tick==null) return;
        Advance((float)delta);
    }
    public void Advance(float dt,FrameIntent? forced=null)
    {
        _clock+=dt;_flash=Math.Max(0,_flash-dt);_shake=Mathf.MoveToward(_shake,0,dt*25);
        Vector2 direction=Input.GetVector("left","right","up","down");
        var cursor=AimOverride!=Vector2.Zero?AimOverride:GetGlobalMousePosition();
        bool inside=Bounds.HasPoint(cursor);
        if(inside) _angle=(cursor-ShipPosition).Angle()+Mathf.Pi/2;
        var intent=forced??new FrameIntent(direction.LengthSquared()>.01f,Input.IsActionJustPressed("dash"),
            inside&&Input.IsActionPressed("primary"),inside&&Input.IsActionPressed("secondary"));
        if(Dead()) intent=new FrameIntent();
        var frame=Tick(dt,intent);
        if(frame.DashStarted)
        {
            _dashDirection=direction.LengthSquared()>.01f?direction:(cursor-ShipPosition).Normalized();
            if(_dashDirection.IsZeroApprox()) _dashDirection=Vector2.Up;
            Audio.Play("dash");Emit(ShipPosition,Style.Cyan,30,180);
        }
        _dashVisual=frame.DashActive;
        if(!Dead())
        {
            if(frame.DashActive) Velocity=_dashDirection*(float)frame.DashSpeed;
            else if(frame.CanSteer) Velocity=Velocity.MoveToward(direction*(float)frame.MoveSpeed,dt*1450);
            ShipPosition+=Velocity*dt;
            var limited=ShipPosition.Clamp(Bounds.Position+new Vector2(28,28),Bounds.End-new Vector2(28,28));
            if(limited.X!=ShipPosition.X) Velocity=new(0,Velocity.Y);
            if(limited.Y!=ShipPosition.Y) Velocity=new(Velocity.X,0);
            ShipPosition=limited;
        }
        if(Velocity.Length()>20 && !Dead())
        {
            var tail=Vector2.FromAngle(_angle+Mathf.Pi/2);
            _sparks.Add(new Spark { Position=ShipPosition+tail*19,Velocity=tail*80-Velocity*.12f+new Vector2(_rng.Next(-12,12),_rng.Next(-12,12)),Life=.2f,MaxLife=.2f,Color=frame.DashActive?Style.Cyan:Style.Amber });
        }
        foreach(var shot in frame.Shots)
        {
            var target=cursor.Clamp(Bounds.Position,Bounds.End);
            var aim=(target-ShipPosition).Normalized();if(aim.IsZeroApprox()) aim=Vector2.Up;
            bool rocket=shot.Kind==ModuleKind.Rocket;
            _projectiles.Add(new Projectile { Position=ShipPosition+aim*22,Direction=aim,Target=target,Speed=(float)shot.Speed,Radius=(float)shot.BlastRadius,Rocket=rocket });
            ShotsFired++;Audio.Play(rocket?"rocket":"shot",rocket?.7f:.5f);
        }
        for(int i=_projectiles.Count-1;i>=0;i--)
        {
            var p=_projectiles[i];p.Age+=dt;
            if(p.Rocket && (p.Position.DistanceTo(p.Target)<=p.Speed*dt+1 || p.Age>5))
            { Explode(p.Target,p.Radius);_projectiles.RemoveAt(i);continue; }
            p.Position+=p.Direction*p.Speed*dt;
            if(!Bounds.Grow(30).HasPoint(p.Position) || p.Age>6) { _projectiles.RemoveAt(i);continue; }
            if(p.Rocket) _sparks.Add(new Spark { Position=p.Position,Velocity=-p.Direction*30,Life=.23f,MaxLife=.23f,Color=Style.Amber });
        }
        for(int i=_sparks.Count-1;i>=0;i--)
        { var p=_sparks[i];p.Life-=dt;p.Position+=p.Velocity*dt;if(p.Life<=0)_sparks.RemoveAt(i); }
        for(int i=_blasts.Count-1;i>=0;i--)
        { _blasts[i].Life-=dt;if(_blasts[i].Life<=0)_blasts.RemoveAt(i); }
        // Bounded transient visuals; weapons themselves are not limited by this cosmetic budget.
        if(_sparks.Count>2500)_sparks.RemoveRange(0,_sparks.Count-2500);
        QueueRedraw();
    }
    private void Explode(Vector2 point,float radius)
    { _blasts.Add(new Blast { Position=point,Life=.5f,Radius=radius });Emit(point,Style.Amber,35,220);Audio.Play("burst",.55f);ExplosionsCreated++; }
    private void Emit(Vector2 point,Color color,int count,float speed)
    {
        for(int i=0;i<count;i++)
        { float lifetime=.2f+(float)_rng.NextDouble()*.4f;_sparks.Add(new Spark { Position=point,Velocity=Vector2.FromAngle((float)_rng.NextDouble()*Mathf.Tau)*speed*(float)_rng.NextDouble(),Life=lifetime,MaxLife=lifetime,Color=color }); }
    }
    public override void _Draw()
    {
        DrawStyleBox(Style.Box(new Color("0b1321"),Style.Border),Bounds);
        for(float x=Bounds.Position.X+24;x<Bounds.End.X;x+=48) for(float y=Bounds.Position.Y+24;y<Bounds.End.Y;y+=48)
            DrawCircle(new(x,y),.65f,new Color("213244"));
        var center=Bounds.GetCenter();
        DrawArc(center,200,0,Mathf.Tau,100,new Color("162635"),1);
        DrawArc(center,270,0,Mathf.Tau,120,new Color("122333"),1);
        DrawLine(center-new Vector2(18,0),center+new Vector2(18,0),new Color("324654"));
        DrawLine(center-new Vector2(0,18),center+new Vector2(0,18),new Color("324654"));
        foreach(var star in _stars)
            DrawCircle(Bounds.Position+star.Pos*Bounds.Size,star.Size,new Color(.65f,.8f,.88f,.18f+.12f*Mathf.Sin(_clock*.45f+star.Phase)));
        DrawString(Style.Mono,Bounds.Position+new Vector2(24,35),"SEKTOR 00   /   SWOBODNY LOT",HorizontalAlignment.Left,-1,12,Style.Muted);
        DrawString(Style.Mono,new Vector2(24,Bounds.End.Y-21),$"POCISKI {ProjectileCount:000}   •   BRAK PRZECIWNIKÓW",HorizontalAlignment.Left,-1,11,Style.Muted);
        foreach(var blast in _blasts)
        {
            float progress=1-blast.Life/.5f,radius=blast.Radius*(.25f+.75f*progress);
            DrawCircle(blast.Position,radius,new Color(Style.Amber,blast.Life*.13f));
            DrawArc(blast.Position,radius,0,Mathf.Tau,60,new Color(Style.Amber,blast.Life*1.5f),2);
        }
        foreach(var p in _sparks)
            DrawRect(new Rect2(p.Position-new Vector2(1,1),new Vector2(2,2)),new Color(p.Color,p.Life/p.MaxLife));
        foreach(var p in _projectiles)
        {
            DrawLine(p.Position-p.Direction*(p.Rocket?15:19),p.Position,p.Rocket?Style.Amber:Style.Cyan,p.Rocket?5:3);
            DrawCircle(p.Position,p.Rocket?2.5f:1.5f,Colors.White);
            if(p.Rocket)
                DrawArc(p.Target,10,0,Mathf.Tau,16,new Color(Style.Amber,.25f),1);
        }
        if(!Dead())
        {
            if(Shield()>0)
            {
                DrawCircle(ShipPosition,39,new Color(Style.Cyan,.035f));
                DrawArc(ShipPosition,39,0,Mathf.Tau,48,new Color(Style.Cyan,.35f),1.5f);
            }
            if(_dashVisual) DrawLine(ShipPosition-_dashDirection*75,ShipPosition,new Color(Style.Cyan,.25f),19);
            DrawSetTransform(ShipPosition+new Vector2(Mathf.Sin(_clock*91)*_shake,0),_angle);
            DrawTextureRect(Art.Player,new Rect2(-30,-32,60,64),false);
            DrawSetTransform(Vector2.Zero);
            DrawCircle(ShipPosition,2,Style.Cyan);
        }
        if(_flash>0) DrawRect(Bounds,new Color(Style.Red,_flash),false,3);
        var mouse=AimOverride!=Vector2.Zero?AimOverride:GetGlobalMousePosition();
        if(Bounds.HasPoint(mouse))
        {
            var c=Style.Cyan;
            DrawArc(mouse,9,.2f,1.3f,8,c,1);DrawArc(mouse,9,1.8f,2.9f,8,c,1);
            DrawArc(mouse,9,3.4f,4.5f,8,c,1);DrawArc(mouse,9,5,6.1f,8,c,1);
            DrawCircle(mouse,1.5f,c);
        }
    }
}
