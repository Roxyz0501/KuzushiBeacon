using System.Numerics;

namespace KuzushiBeacon;

public interface IZantetsukenAction
{
    void BeginAttempt() { }
    string SelectionFailure => "range, LoS or action unavailable";
    string FailureReason => "action rejected";
    bool CanUse(Actor target);
    bool TryExecute(Actor target, uint owner);
}

public static class Zantetsuken
{
    public const uint ActionId = 29537;
    public const float Radius = 5;

    // A fresh, bounded snapshot per explicit invocation, independent of overlay settings/cache.
    public static bool Execute(bool allowed, uint owner, Vector3 origin, ulong currentTarget,
        IHighlightWorld world, IZantetsukenAction action, int minimum = 1, Func<Actor,bool>? include = null, Action<string>? failure = null)
    {
        bool Fail(string reason){failure?.Invoke(reason);return false;}
        if (!allowed || !Rules.ValidId(owner) || !Finite(origin)) return Fail("conditions");
        action.BeginAttempt();
        var excluded = 0;
        var candidates = new List<Actor>();
        int visited = 0;
        foreach (var actor in world.Players)
        {
            if (visited++ >= Tracker.MaxCandidates) break;
            if (!Rules.ValidId(actor.EntityId) || actor.EntityId == owner || !actor.Alive
                || !actor.Targetable || !Finite(actor.Position)) continue;
            var remaining = world.OwnKuzushiRemaining(actor.Index, owner);
            if (!float.IsFinite(remaining) || remaining <= 0) continue;
            if (include != null && !include(actor)) { excluded++; continue; }
            candidates.Add(actor);
        }
        var selection = FindBest(candidates, origin, currentTarget, action.CanUse);
        if(candidates.Count==0)return Fail(excluded > 0 ? "excluded" : "no eligible Kuzushi");
        if(selection.Target is not {} selected)return Fail(action.SelectionFailure);
        if(selection.Count<minimum)return Fail("minimum coverage");
        return action.TryExecute(selected,owner)||Fail(action.FailureReason);
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
