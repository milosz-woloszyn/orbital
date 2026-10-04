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
    public CombatWorld World { get; private set; }=new(autoSpawn:false);
    public bool CombatEnabled { get; private set; }
    public event Action? FrameFinished;
    public int ProjectileCount=>World.Bullets.Count;
    public int ShotsFired=>World.ShotsFired;
    public int ExplosionsCreated=>World.Explosions;
    public Vector2 AimOverride { get; set; }=Vector2.Zero;
    private Vector2 _dashDirection;
    private float _angle,_clock,_flash,_shake;
    private bool _dashVisual;
    private readonly List<Spark> _sparks=new();
    private readonly List<Blast> _blasts=new();
    private readonly List<RewardText> _rewards=new();
    private sealed class RewardText { public Vector2 Position;public string Text="";public float Life=.9f; }
    public void RewardFeedback(EnemyReward reward)=>_rewards.Add(new RewardText { Position=Visual(reward.Position),Text=$"+{reward.Experience} EXP  +{reward.Materials} M" });
    private readonly Random _rng=new(328);
    private readonly List<(Vector2 Pos,float Size,float Phase)> _stars=new();
    private sealed class Spark
    { public Vector2 Position,Velocity; public float Life,MaxLife; public Color Color; }
    private sealed class Blast { public Vector2 Position; public float Life,Radius; public bool Hostile,Kill; }
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore; TextureFilter=TextureFilterEnum.Nearest;
        for(int i=0;i<260;i++) _stars.Add((new Vector2((float)_rng.NextDouble(),(float)_rng.NextDouble()),(float)_rng.NextDouble()*1.3f+.4f,(float)_rng.NextDouble()*6));
    }
    public void Reset()
    { ShipPosition=new(Bounds.GetCenter().X,Bounds.End.Y-156);Velocity=Vector2.Zero;World=new CombatWorld(System.Environment.TickCount,CombatEnabled);_flash=0;_shake=0;_sparks.Clear();_blasts.Clear();_rewards.Clear();_dashVisual=false; }
    public void SetCombat(bool enabled,int seed)
    { CombatEnabled=enabled;Reset();World=new CombatWorld(seed,enabled); }
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
        if(!Dead())
        {
            World.Step(dt,Numeric(ShipPosition),Numeric(Velocity),Numeric(cursor),frame.Shots,
                new ArenaBounds(Bounds.Position.X,Bounds.Position.Y,Bounds.Size.X,Bounds.Size.Y));
            foreach(var shot in frame.Shots)
                Audio.Play(shot.Kind is ModuleKind.Rocket or ModuleKind.GuidedRocket?"rocket":"shot",shot.Kind==ModuleKind.Laser?.12f:.45f);
            foreach(var effect in World.Effects)
            {
                var point=Visual(effect.Position);
                _blasts.Add(new Blast { Position=point,Life=.45f,Radius=effect.Radius,Hostile=effect.Hostile,Kill=effect.Kill });
                Emit(point,effect.Hostile?Style.Red:Style.Amber,effect.Kill?22:32,effect.Kill?140:220);
                Audio.Play("burst",effect.Kill?.22f:.5f);
                if(effect.Kill)_shake=Math.Max(_shake,1.8f);
            }
        }
        for(int i=_sparks.Count-1;i>=0;i--)
        { var p=_sparks[i];p.Life-=dt;p.Position+=p.Velocity*dt;if(p.Life<=0)_sparks.RemoveAt(i); }
        for(int i=_blasts.Count-1;i>=0;i--)
        { _blasts[i].Life-=dt;if(_blasts[i].Life<=0)_blasts.RemoveAt(i); }
        // Bounded transient visuals; weapons themselves are not limited by this cosmetic budget.
        if(_sparks.Count>2500)_sparks.RemoveRange(0,_sparks.Count-2500);
        foreach(var reward in _rewards) { reward.Life-=dt;reward.Position+=new Vector2(0,-22*dt); }
        _rewards.RemoveAll(r=>r.Life<=0);
        FrameFinished?.Invoke();QueueRedraw();
    }
    private static System.Numerics.Vector2 Numeric(Vector2 v)=>new(v.X,v.Y);
    private static Vector2 Visual(System.Numerics.Vector2 v)=>new(v.X,v.Y);
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
        DrawString(Style.Mono,Bounds.Position+new Vector2(24,35),CombatEnabled?$"ORBITA {(int)(World.Time/60)+1:00}   /   PRZETRWAJ":"SEKTOR 00   /   SWOBODNY LOT",HorizontalAlignment.Left,-1,12,Style.Muted);
        DrawString(Style.Mono,new Vector2(24,Bounds.End.Y-21),CombatEnabled?$"WROGOWIE {World.Enemies.Count:00}   •   OSTRZAŁ {ProjectileCount:000}":$"POCISKI {ProjectileCount:000}   •   LABORATORIUM",HorizontalAlignment.Left,-1,11,Style.Muted);
        foreach(var blast in _blasts)
        {
            float progress=1-blast.Life/.5f,radius=blast.Radius*(.25f+.75f*progress);
            var blastColor=blast.Hostile?Style.Red:Style.Amber;
            DrawCircle(blast.Position,radius,new Color(blastColor,blast.Life*.13f));
            DrawArc(blast.Position,radius,0,Mathf.Tau,60,new Color(blastColor,blast.Life*1.5f),2);
        }
        foreach(var p in _sparks)
            DrawRect(new Rect2(p.Position-new Vector2(1,1),new Vector2(2,2)),new Color(p.Color,p.Life/p.MaxLife));
        DrawCombat();
        foreach(var reward in _rewards)DrawString(Style.Mono,reward.Position-new Vector2(55,0),reward.Text,HorizontalAlignment.Left,-1,12,new Color(Style.Amber,Math.Min(1,reward.Life*2)));
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
