using System.Numerics;

namespace KuzushiBeacon;

public interface IZantetsukenAction
{
    bool CanUse(Actor target);
    bool TryExecute(Actor target, uint owner);
}

public static class Zantetsuken
{
    public const uint ActionId = 29537;
    public const float Radius = 5;

    // A fresh, bounded snapshot per explicit invocation, independent of overlay settings/cache.
    public static bool Execute(bool allowed, uint owner, Vector3 origin, ulong currentTarget,
        IHighlightWorld world, IZantetsukenAction action)
    {
        if (!allowed || !Rules.ValidId(owner) || !Finite(origin)) return false;
        var candidates = new List<Actor>();
        int visited = 0;
        foreach (var actor in world.Players)
        {
            if (visited++ >= Tracker.MaxCandidates) break;
            if (!Rules.ValidId(actor.EntityId) || actor.EntityId == owner || !actor.Alive
                || !actor.Targetable || !Finite(actor.Position)) continue;
            var remaining = world.OwnKuzushiRemaining(actor.Index, owner);
            if (float.IsFinite(remaining) && remaining > 0) candidates.Add(actor);
        }
        var selection = FindBest(candidates, origin, currentTarget, action.CanUse);
        return selection.Target is { } selected && action.TryExecute(selected, owner);
    }

    public static (Actor? Target, int Count) FindBest(IReadOnlyList<Actor> candidates, Vector3 origin,
        ulong currentTarget, Func<Actor, bool> canSelect)
    {
        Actor? best = null;
        int bestCount = -1;
        float bestDistance = float.MaxValue;
        foreach (var center in candidates)
        {
            if (!canSelect(center)) continue;
            int count = 0;
            foreach (var other in candidates)
                if (Vector3.DistanceSquared(center.Position, other.Position) <= Radius * Radius) count++;
            var distance = Vector3.DistanceSquared(origin, center.Position);
            bool preferred = best is null || (center.ObjectId == currentTarget && best.Value.ObjectId != currentTarget)
                || ((center.ObjectId == currentTarget) == (best.Value.ObjectId == currentTarget)
                    && (distance < bestDistance || (distance == bestDistance && center.EntityId < best.Value.EntityId)));
            if (count > bestCount || (count == bestCount && preferred))
            { best = center; bestCount = count; bestDistance = distance; }
        }
        return (best, Math.Max(0, bestCount));
    }

    public static bool FullGauge(bool pvp, int current, int barUnits, int bars)
        => pvp && bars > 0 && barUnits > 0 && current >= barUnits;

    public static bool ShowCount(bool fullGauge, int count, double expiresAt, double now)
        => fullGauge && count > 0 && now < expiresAt;

    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
