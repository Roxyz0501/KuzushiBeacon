using System.Numerics;

namespace KuzushiBeacon;

public readonly record struct HpCandidate(Actor Actor, uint Hp, float Radius, bool Enemy);
public static class LowHpTarget
{
    public static bool InRange(Vector3 origin, float ownRadius, Vector3 target, float targetRadius, float range)
    {
        if (!float.IsFinite(ownRadius) || !float.IsFinite(targetRadius) || !float.IsFinite(range)
            || ownRadius < 0 || targetRadius < 0 || range < 0) return false;
        var distance = Vector3.Distance(origin, target);
        return float.IsFinite(distance) && distance <= range + ownRadius + targetRadius;
    }

    public static HpCandidate? Find(IEnumerable<HpCandidate> candidates, uint owner, Vector3 origin, float ownRadius, float range)
    {
        HpCandidate? best = null;
        float bestDistance = float.MaxValue;
        int visited = 0;
        foreach (var candidate in candidates)
        {
            if (visited++ >= Tracker.MaxCandidates) break;
            var actor = candidate.Actor;
            if (!candidate.Enemy || candidate.Hp == 0 || !actor.Alive || !actor.Targetable
                || !Rules.ValidId(actor.EntityId) || actor.EntityId == owner
                || !InRange(origin, ownRadius, actor.Position, candidate.Radius, range)) continue;
            var distance = Vector3.DistanceSquared(origin, actor.Position);
            if (best is null || candidate.Hp < best.Value.Hp || (candidate.Hp == best.Value.Hp
                && (distance < bestDistance || (distance == bestDistance && actor.EntityId < best.Value.Actor.EntityId))))
            { best = candidate; bestDistance = distance; }
        }
        return best;
    }
}
