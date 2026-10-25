using System.Numerics;

namespace KuzushiBeacon;

public readonly record struct Observation(Actor Actor, uint Hp, uint MaxHp, float Radius, bool Enemy, bool Ally,
    ulong Target, float OwnKuzushi, float OtherKuzushi, bool Guard, bool Invulnerable, float HpRate, bool Healed)
{
    public float Percent => MaxHp == 0 ? 100 : 100f * Hp / MaxHp;
    public float Distance(Vector3 origin, float radius) => Math.Max(0,Vector3.Distance(origin,Actor.Position)-radius-Radius);
    public bool Eligible(bool guard,bool invulnerable) => Actor.Alive && Actor.Targetable && !(guard&&Guard) && !(invulnerable&&Invulnerable);
}
public readonly record struct Coverage(Observation Center, int Count, int LowCount);
public static class SupportMath
{
    public static Coverage Count(Observation center,IReadOnlyList<Observation> enemies,float radius,float lowPercent,bool excludeGuard=false,bool excludeInvulnerable=false)
    {
        int count=0,low=0;
        foreach(var e in enemies)
            if(e.Enemy && e.Eligible(excludeGuard,excludeInvulnerable) && Vector3.DistanceSquared(e.Actor.Position,center.Actor.Position)<=radius*radius)
            { count++; if(e.Percent<=lowPercent) low++; }
        return new(center,count,low);
    }
    public static Vector3 Retreat(Vector3 origin,IReadOnlyList<Observation> actors,float radius)
    {
        var best=Vector3.Zero; var bestScore=float.NegativeInfinity;
        for(int i=0;i<16;i++)
        {
            var angle=i*MathF.Tau/16;var dir=new Vector3(MathF.Cos(angle),0,MathF.Sin(angle));
            var point=origin+dir*radius;float score=0;
            foreach(var a in actors)
            { var weight=1/(1+Vector3.Distance(point,a.Actor.Position));score+=(a.Ally?1:a.Enemy?-1:0)*weight; }
            if(score>bestScore) { bestScore=score;best=dir; }
        }
        return best;
    }
}

public sealed class AlertLatch
{
    private readonly Dictionary<int,(double Since,bool Fired)> states=[];
    public bool Observe(int id,bool condition,double now,double stable)
    {
        if(!condition){states.Remove(id);return false;}
        if(!states.TryGetValue(id,out var state))state=(now,false);
        if(!state.Fired && now-state.Since>=stable){states[id]=(state.Since,true);return true;}
        states[id]=state;return false;
    }
    public void Clear()=>states.Clear();
}

internal sealed class SnapshotWorld(IReadOnlyList<Observation> observations,uint owner,double age) : IHighlightWorld
{
    public IEnumerable<Actor> Players => observations.Select(o=>o.Actor);
    public float OwnKuzushiRemaining(int index,uint sourceId) => sourceId!=owner?0:
        Math.Max(0,observations.FirstOrDefault(o=>o.Actor.Index==index).OwnKuzushi-(float)age);
}
