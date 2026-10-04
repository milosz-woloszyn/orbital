using System.Numerics;

namespace Orbital.Domain;

public enum EnemyKind { Basic, Fan, Rocket, Carrier, Looper, Minion }
public enum BulletMotion { Straight, Rocket, Loop, Guided, Seeker, Bomb }
public readonly record struct ArenaBounds(float Left,float Top,float Width,float Height)
{
    public Vector2 Min=>new(Left,Top);
    public Vector2 Max=>Min+new Vector2(Width,Height);
    public bool Contains(Vector2 p,float margin=0)=>p.X>=Left-margin && p.X<=Left+Width+margin && p.Y>=Top-margin && p.Y<=Top+Height+margin;
    public Vector2 Clamp(Vector2 p,float inset=0)=>Vector2.Clamp(p,Min+new Vector2(inset),Max-new Vector2(inset));
}
public sealed class Enemy
{
    public int Id { get; init; }
    public EnemyKind Kind { get; init; }
    public Vector2 Position, Velocity;
    public float Health, MaxHealth, Radius, Age, FireTimer, Flash, Orbit, ContactTimer;
    public int ParentId;
    public bool Alive=>Health>0;
}
public sealed class CombatBullet
{
    public Vector2 Position, Direction, Target;
    public float Speed,Damage,Radius,BlastRadius,Age,Phase;
    public bool Hostile;
    public BulletMotion Motion;
    public int TargetId;
}
public sealed class Beam
{
    public Vector2 Start,End;
    public float Life;
    public int ModuleId;
}
public readonly record struct CombatEffect(Vector2 Position,float Radius,bool Hostile,bool Kill=false);
public readonly record struct EnemyReward(int Experience,int Materials,Vector2 Position);

/// <summary>Combat independent of rendering: swept collisions, deterministic AI and bounded lifetimes.</summary>
public sealed class CombatWorld
{
    private readonly Random random;
    private readonly List<Enemy> enemies=new();
    private readonly List<CombatBullet> bullets=new();
    private readonly List<Beam> beams=new();
    private readonly List<CombatEffect> effects=new();
    private int nextEnemy=1;
    private float spawnTimer=1.4f,hitGrace;
    public CombatWorld(int seed=1,bool autoSpawn=true) { random=new Random(seed);AutoSpawn=autoSpawn; }
    public bool AutoSpawn { get; set; }
    public float Time { get; private set; }
    public float Threat=>1+Time/65;
    public IReadOnlyList<Enemy> Enemies=>enemies;
    public IReadOnlyList<CombatBullet> Bullets=>bullets;
    public IReadOnlyList<Beam> Beams=>beams;
    public IReadOnlyList<CombatEffect> Effects=>effects;
    public int Kills { get; private set; }
    public int ShotsFired { get; private set; }
    public int Explosions { get; private set; }
    public event Action<EnemyReward>? Rewarded;
    public event Action<float>? PlayerDamaged;
    public Enemy Spawn(EnemyKind kind,Vector2 position,int parentId=0)
    {
        (float health,float radius)=kind switch
        {
            EnemyKind.Basic=>(32,18),EnemyKind.Fan=>(60,24),EnemyKind.Rocket=>(80,24),
            EnemyKind.Carrier=>(160,34),EnemyKind.Looper=>(75,23),_=>(20,12)
        };
        var enemy=new Enemy { Id=nextEnemy++,Kind=kind,Position=position,ParentId=parentId,
            Health=health*(1+Time/240),MaxHealth=health*(1+Time/240),Radius=radius,
            FireTimer=.85f+(float)random.NextDouble()*.9f,Orbit=(float)random.NextDouble()*MathF.Tau,
            Velocity=new(random.Next(2)==0?-100:100,0) };
        enemies.Add(enemy);
        if(kind==EnemyKind.Carrier)
            for(int i=0;i<3;i++) { var minion=Spawn(EnemyKind.Minion,position,parentId:enemy.Id);minion.Orbit=i*MathF.Tau/3; }
        return enemy;
    }
    public void ClearHostileBullets() { bullets.RemoveAll(b=>b.Hostile);hitGrace=0; }
    public void ClearEffects()=>effects.Clear();
    public void Step(float dt,Vector2 player,Vector2 velocity,Vector2 cursor,IReadOnlyList<WeaponShot> shots,ArenaBounds bounds)
    {
        if(!float.IsFinite(dt) || dt<0 || dt>.25f)throw new ArgumentOutOfRangeException(nameof(dt));
        effects.Clear();Time+=dt;hitGrace=Math.Max(0,hitGrace-dt);
        foreach(var beam in beams) { beam.Life-=dt;beam.Start=player;beam.End=player+SafeDirection(cursor-player)*MathF.Sqrt(bounds.Width*bounds.Width+bounds.Height*bounds.Height); }
        beams.RemoveAll(b=>b.Life<=0);
        spawnTimer-=dt;
        if(AutoSpawn && spawnTimer<=0 && enemies.Count<90)
        {
            spawnTimer=Math.Max(.23f,1.65f/(1+Time/45));
            int unlocked=Math.Min(5,1+(int)(Time/13));
            var kind=(EnemyKind)random.Next(unlocked);
            float x=bounds.Left+35+(float)random.NextDouble()*Math.Max(1,bounds.Width-70);
            Spawn(kind,new Vector2(x,bounds.Top-55));
        }
        foreach(var enemy in enemies)
        {
            if(!enemy.Alive)continue;
            enemy.Age+=dt;enemy.Flash=Math.Max(0,enemy.Flash-dt);enemy.ContactTimer-=dt;
            var aim=SafeDirection(player-enemy.Position);
            if(enemy.Kind==EnemyKind.Minion)
            {
                var parent=enemies.FirstOrDefault(e=>e.Id==enemy.ParentId && e.Alive);
                if(parent==null) { Kill(enemy);continue; }
                enemy.Orbit+=dt*.8f;
                enemy.Position=parent.Position+FromAngle(enemy.Orbit)*72;
            }
            else if(enemy.Position.Y<bounds.Top+50)enemy.Position+=new Vector2(0,75*dt);
            else if(enemy.Kind==EnemyKind.Basic)
                enemy.Position+=aim*(65+Math.Min(65,Time*.18f))*dt;
            else if(enemy.Kind==EnemyKind.Fan)
            {
                if(enemy.Position.X<bounds.Left+30)enemy.Velocity=new(110,0);
                if(enemy.Position.X>bounds.Max.X-30)enemy.Velocity=new(-110,0);
                enemy.Position+=new Vector2(enemy.Velocity.X,MathF.Sin(enemy.Age)*12)*dt;
            }
            else
            {
                var destination=new Vector2(bounds.Left+bounds.Width*(.5f+.34f*MathF.Sin(enemy.Age*.26f+enemy.Id)),bounds.Top+110+(enemy.Id%3)*55);
                enemy.Position+=SafeDirection(destination-enemy.Position)*Math.Min(55,Vector2.Distance(destination,enemy.Position))*dt;
            }
            if(enemy.ContactTimer<=0 && Vector2.DistanceSquared(player,enemy.Position)<MathF.Pow(enemy.Radius+7,2))
            { HitPlayer(14);enemy.ContactTimer=.65f; }
            enemy.FireTimer-=dt;
            if(enemy.FireTimer<=0 && bounds.Contains(enemy.Position))
            {
                float pace=Math.Max(.45f,1-Time/360);
                switch(enemy.Kind)
                {
                    case EnemyKind.Basic:
                        Hostile(enemy.Position,aim,210,9);enemy.FireTimer=1.3f*pace;break;
                    case EnemyKind.Fan:
                        for(int i=-3;i<=3;i++)Hostile(enemy.Position,Rotate(aim,i*.17f),180,10);
                        enemy.FireTimer=1.9f*pace;break;
                    case EnemyKind.Rocket:
                        var target=bounds.Clamp(player+velocity*.65f,20);
                        bullets.Add(new CombatBullet { Hostile=true,Motion=BulletMotion.Rocket,Position=enemy.Position,
                            Direction=SafeDirection(target-enemy.Position),Target=target,Speed=265,Damage=25,Radius=6,BlastRadius=70 });
                        enemy.FireTimer=2.4f*pace;break;
                    case EnemyKind.Carrier:
                        for(int i=-1;i<=1;i++)Hostile(enemy.Position,Rotate(aim,i*.14f),190,12);
                        enemy.FireTimer=1.8f*pace;break;
                    case EnemyKind.Minion:
                        for(int i=0;i<6;i++)Hostile(enemy.Position,FromAngle(i*MathF.Tau/6+enemy.Age*.18f),140,7);
                        enemy.FireTimer=2.6f*pace;break;
                    case EnemyKind.Looper:
                        for(int i=-1;i<=1;i++)Hostile(enemy.Position,Rotate(aim,i*.28f),125,10,BulletMotion.Loop);
                        enemy.FireTimer=1.65f*pace;break;
                }
            }
        }
        foreach(var shot in shots)Fire(shot,player,cursor,bounds);
        for(int i=bullets.Count-1;i>=0;i--)
        {
            var b=bullets[i];var old=b.Position;b.Age+=dt;
            if(b.Motion==BulletMotion.Bomb)
            {
                if(b.Age>=.85f) { Explode(b,b.Position,player);bullets.RemoveAt(i); }
                continue;
            }
            if(b.Motion==BulletMotion.Guided) { b.Target=bounds.Clamp(cursor);b.Direction=SafeDirection(b.Target-b.Position); }
            if(b.Motion==BulletMotion.Seeker)
            {
                var target=enemies.FirstOrDefault(e=>e.Id==b.TargetId && e.Alive)??Nearest(b.Position);
                if(target!=null) { b.TargetId=target.Id;b.Direction=SafeDirection(target.Position-b.Position); }
            }
            b.Position+=b.Direction*b.Speed*dt;
            if(b.Motion==BulletMotion.Loop)
            {
                float previous=b.Phase;b.Phase+=dt*7;
                b.Position+=(FromAngle(b.Phase)-FromAngle(previous))*24;
            }
            bool remove=false;
            if(b.Motion is BulletMotion.Rocket or BulletMotion.Guided &&
                SegmentHit(old,b.Position,b.Target,Math.Max(2,b.Speed*dt*.1f)))
            { Explode(b,b.Target,player);remove=true; }
            if(!remove && b.Hostile && SegmentHit(old,b.Position,player,b.Radius+6))
            {
                if(b.Motion==BulletMotion.Rocket)Explode(b,b.Position,player);else HitPlayer(b.Damage);
                remove=true;
            }
            else if(!remove && !b.Hostile)
            {
                Enemy? hit=null;float distance=float.MaxValue;
                foreach(var enemy in enemies)
                    if(enemy.Alive && SegmentHit(old,b.Position,enemy.Position,enemy.Radius+b.Radius))
                    { float d=Vector2.DistanceSquared(old,enemy.Position);if(d<distance) { hit=enemy;distance=d; } }
                if(hit!=null)
                {
                    if(b.BlastRadius>0)Explode(b,hit.Position,player);else DamageEnemy(hit,b.Damage);
                    remove=true;
                }
            }
            if(remove || b.Age>9 || !bounds.Contains(b.Position,180))bullets.RemoveAt(i);
        }
        enemies.RemoveAll(e=>!e.Alive);
    }
    private void Fire(WeaponShot shot,Vector2 player,Vector2 cursor,ArenaBounds bounds)
    {
        var aim=SafeDirection(cursor-player);ShotsFired++;
        if(shot.Kind==ModuleKind.Laser)
        {
            var end=player+aim*MathF.Sqrt(bounds.Width*bounds.Width+bounds.Height*bounds.Height);
            var beam=beams.FirstOrDefault(b=>b.ModuleId==shot.ModuleId);
            if(beam==null) { beam=new Beam { ModuleId=shot.ModuleId };beams.Add(beam); }
            beam.Start=player;beam.End=end;beam.Life=.065f;
            foreach(var enemy in enemies)
                if(enemy.Alive && SegmentHit(player,end,enemy.Position,enemy.Radius+4))DamageEnemy(enemy,(float)shot.Damage);
            return;
        }
        int count=shot.Kind==ModuleKind.FanBlaster?5:1;
        for(int i=0;i<count;i++)
        {
            var direction=Rotate(aim,(i-(count-1)*.5f)*.14f);
            var motion=shot.Kind switch { ModuleKind.Rocket=>BulletMotion.Rocket,ModuleKind.GuidedRocket=>BulletMotion.Guided,
                ModuleKind.Seeker=>BulletMotion.Seeker,ModuleKind.Bomb=>BulletMotion.Bomb,_=>BulletMotion.Straight };
            bullets.Add(new CombatBullet { Position=player+(motion==BulletMotion.Bomb?Vector2.Zero:direction*12),
                Direction=direction,Target=bounds.Clamp(cursor),Speed=(float)shot.Speed,Damage=(float)shot.Damage,
                BlastRadius=(float)shot.BlastRadius,Radius=motion==BulletMotion.Straight?3:5,Motion=motion });
        }
    }
    private void Hostile(Vector2 position,Vector2 direction,float speed,float damage,BulletMotion motion=BulletMotion.Straight)
    {
        // Density is controlled by the director; this last guard protects extreme endurance runs.
        if(bullets.Count>=6000)return;
        bullets.Add(new CombatBullet { Position=position,Direction=direction,Speed=speed,Damage=damage,
            Radius=4,Hostile=true,Motion=motion });
    }
    public Enemy? Nearest(Vector2 position)
    {
        Enemy? result=null;float best=float.MaxValue;
        foreach(var enemy in enemies)if(enemy.Alive)
        { float d=Vector2.DistanceSquared(position,enemy.Position);if(d<best) { best=d;result=enemy; } }
        return result;
    }
    public void DamageEnemy(Enemy enemy,float amount)
    {
        if(!enemy.Alive || amount<=0)return;
        enemy.Health-=amount;enemy.Flash=.09f;
        if(enemy.Health<=0)Kill(enemy);
    }
    private void Kill(Enemy enemy)
    {
        // A negative sentinel makes carrier cascades and simultaneous blasts idempotent.
        if(enemy.Health==float.NegativeInfinity)return;
        enemy.Health=float.NegativeInfinity;Kills++;
        (int xp,int cash)=enemy.Kind switch
        {
            EnemyKind.Basic=>(12,12),EnemyKind.Fan=>(18,17),EnemyKind.Rocket=>(23,21),
            EnemyKind.Carrier=>(35,32),EnemyKind.Looper=>(22,20),_=>(5,5)
        };
        Rewarded?.Invoke(new(xp,cash,enemy.Position));effects.Add(new(enemy.Position,enemy.Radius*1.7f,false,true));
        if(enemy.Kind==EnemyKind.Carrier)
            foreach(var minion in enemies)if(minion.ParentId==enemy.Id && minion.Alive)Kill(minion);
    }
    private void Explode(CombatBullet bullet,Vector2 at,Vector2 player)
    {
        Explosions++;effects.Add(new(at,bullet.BlastRadius,bullet.Hostile));
        if(bullet.Hostile)
        { if(Vector2.DistanceSquared(player,at)<=MathF.Pow(bullet.BlastRadius+6,2))HitPlayer(bullet.Damage); }
        else foreach(var enemy in enemies)
            if(enemy.Alive && Vector2.DistanceSquared(enemy.Position,at)<=MathF.Pow(bullet.BlastRadius+enemy.Radius,2))DamageEnemy(enemy,bullet.Damage);
    }
    private void HitPlayer(float damage)
    {
        if(hitGrace>0)return;
        hitGrace=.09f;PlayerDamaged?.Invoke(damage);
    }
    public static bool SegmentHit(Vector2 from,Vector2 to,Vector2 point,float radius)
    {
        var segment=to-from;float length=segment.LengthSquared();
        float t=length<.00001f?0:Math.Clamp(Vector2.Dot(point-from,segment)/length,0,1);
        return Vector2.DistanceSquared(from+segment*t,point)<=radius*radius;
    }
    private static Vector2 SafeDirection(Vector2 v)=>v.LengthSquared()<.00001f?new(0,-1):Vector2.Normalize(v);
    private static Vector2 FromAngle(float a)=>new(MathF.Cos(a),MathF.Sin(a));
    private static Vector2 Rotate(Vector2 v,float a)=>new(v.X*MathF.Cos(a)-v.Y*MathF.Sin(a),v.X*MathF.Sin(a)+v.Y*MathF.Cos(a));
}
