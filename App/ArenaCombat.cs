using Godot;
using Orbital.Domain;

namespace Orbital;

public partial class Arena
{
    private void DrawCombat()
    {
        foreach(var enemy in World.Enemies)
        {
            if(!enemy.Alive)continue;
            var position=Visual(enemy.Position);
            if(!Bounds.HasPoint(position))continue;
            var tint=enemy.Kind switch { EnemyKind.Looper=>new Color("d19cff"),EnemyKind.Minion=>new Color("ffa278"),_=>Colors.White };
            if(enemy.Flash>0)tint=new Color(2,2,2);
            float angle=enemy.Kind==EnemyKind.Basic?(ShipPosition-position).Angle()-Mathf.Pi/2:0;
            float size=enemy.Radius*2.8f;
            DrawSetTransform(position,angle);
            DrawTextureRect(Art.EnemyTexture(enemy.Kind),new Rect2(-size/2,-size/2,size,size),false,tint);
            DrawSetTransform(Vector2.Zero);
            if(enemy.Kind==EnemyKind.Carrier)DrawArc(position,72,0,Mathf.Tau,60,new Color(Style.Red,.17f),1);
            if(enemy.Health<enemy.MaxHealth)
            {
                DrawRect(new Rect2(position.X-enemy.Radius,position.Y-enemy.Radius-12,enemy.Radius*2,3),Style.Background);
                DrawRect(new Rect2(position.X-enemy.Radius,position.Y-enemy.Radius-12,enemy.Radius*2*enemy.Health/enemy.MaxHealth,3),Style.Red);
            }
            if(enemy.FireTimer<.25f)
                DrawArc(position,enemy.Radius+6,0,Mathf.Tau,24,new Color(Style.Amber,.45f),1.5f);
        }
        foreach(var beam in World.Beams)
        {
            var start=Visual(beam.Start);var end=Visual(beam.End);
            var ray=(end-start).Normalized();
            // Draw only as far as the arena edge, while collision uses the complete ray.
            float length=12000;
            if(ray.X>0)length=Math.Min(length,(Bounds.End.X-start.X)/ray.X);
            if(ray.X<0)length=Math.Min(length,(Bounds.Position.X-start.X)/ray.X);
            if(ray.Y>0)length=Math.Min(length,(Bounds.End.Y-start.Y)/ray.Y);
            if(ray.Y<0)length=Math.Min(length,(Bounds.Position.Y-start.Y)/ray.Y);
            end=start+ray*Math.Max(0,length);
            DrawLine(start,end,new Color(Style.Cyan,.14f),18);DrawLine(start,end,Style.Cyan,5);
            DrawLine(start,end,Colors.White,2);DrawCircle(start,6,Style.Cyan);
        }
        foreach(var bullet in World.Bullets)
        {
            var p=Visual(bullet.Position);var direction=Visual(bullet.Direction);
            if(bullet.Motion==BulletMotion.Bomb)
            {
                DrawArc(p,bullet.BlastRadius,0,Mathf.Tau,64,new Color(Style.Cyan,.12f),1);
                DrawCircle(p,9,Style.Background);DrawArc(p,11,0,Mathf.Tau*Math.Min(1,bullet.Age/.85f),24,Style.Amber,3);
                DrawRect(new Rect2(p-new Vector2(4,4),new Vector2(8,8)),Style.Amber);continue;
            }
            if(bullet.Motion is BulletMotion.Rocket or BulletMotion.Guided)
            {
                var target=Visual(bullet.Target);var color=bullet.Hostile?Style.Red:Style.Amber;
                if(bullet.Hostile)
                {
                    DrawCircle(target,bullet.BlastRadius,new Color(color,.045f));
                    DrawArc(target,bullet.BlastRadius,0,Mathf.Tau,48,new Color(color,.35f),1.5f);
                    DrawLine(target-new Vector2(9,0),target+new Vector2(9,0),color,1);
                    DrawLine(target-new Vector2(0,9),target+new Vector2(0,9),color,1);
                }
                else DrawArc(target,9,0,Mathf.Tau,16,new Color(color,.25f),1);
            }
            if(!Bounds.HasPoint(p))continue;
            if(bullet.Hostile)
            {
                var color=bullet.Motion==BulletMotion.Loop?new Color("df8cff"):new Color("ff866d");
                DrawCircle(p,bullet.Radius+3,new Color(color,.15f));DrawCircle(p,bullet.Radius+1,Style.Background);
                DrawCircle(p,bullet.Radius,color);DrawCircle(p,1.4f,new Color("fff1d7"));
            }
            else
            {
                var color=bullet.Motion is BulletMotion.Rocket or BulletMotion.Guided?Style.Amber:bullet.Motion==BulletMotion.Seeker?Style.Green:Style.Cyan;
                DrawLine(p-direction*18,p,new Color(color,.2f),8);DrawLine(p-direction*14,p,color,3);DrawCircle(p,1.6f,Colors.White);
            }
        }
    }
}
