using System.Numerics;

namespace KuzushiBeacon;

public readonly record struct Actor(int Index, uint EntityId, ulong ObjectId, Vector3 Position, bool Alive, bool Targetable);
public readonly record struct MarkedActor(Actor Actor, double ExpiresAt);

public interface IHighlightWorld
{
    IEnumerable<Actor> Players { get; }
    float OwnKuzushiRemaining(int index, uint sourceId);
}

public static class Rules
{
    public const uint KuzushiStatus = 3202;
    public const uint SamuraiJob = 34;
    public static bool ValidId(uint id) => id != 0 && id != 0xE0000000;
    public static bool CanRun(bool enabled, bool line, bool marker, bool loggedIn, bool pvpExcludingDen, uint job)
        => enabled && (line || marker) && loggedIn && pvpExcludingDen && job == SamuraiJob;
    public static bool OwnStatus(uint status, uint source, uint player, float remaining)
        => status == KuzushiStatus && ValidId(player) && source == player && float.IsFinite(remaining) && remaining > 0;
}

// Bounded, ephemeral cache. Contains values only, never native addresses or game wrappers.
public sealed class Tracker
{
    public const int MaxTargets = 200;
    public const int MaxCandidates = 200;
    private readonly List<MarkedActor> targets = new(MaxTargets);
    private double nextScan;
    private uint owner;
    public IReadOnlyList<MarkedActor> Targets => targets;
    public long ScanCount { get; private set; }
    public long CandidateReads { get; private set; }
    public long StatusListReads { get; private set; }
    public double LastScanMs { get; private set; }

    public void Clear()
    {
        targets.Clear();
        nextScan = 0;
        owner = 0;
    }

    public void Update(bool active, double now, uint player, Vector3 position, float distance,
        int intervalMs, IHighlightWorld world)
    {
        if (!active || !Rules.ValidId(player)) { Clear(); return; }
        if (owner != player) Clear();
        owner = player;
        if (now < nextScan) return;
        nextScan = now + Math.Clamp(intervalMs, 50, 500) / 1000d;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        targets.Clear();
        ScanCount++;
        int count = 0;
        foreach (var actor in world.Players)
        {
            if (count++ >= MaxCandidates) break;
            CandidateReads++;
            if (!Rules.ValidId(actor.EntityId) || actor.EntityId == player || !actor.Alive || !actor.Targetable
                || !float.IsFinite(Vector3.DistanceSquared(actor.Position, position))
                || Vector3.DistanceSquared(actor.Position, position) > distance * distance) continue;
            StatusListReads++;
            var remaining = world.OwnKuzushiRemaining(actor.Index, player);
            if (!float.IsFinite(remaining) || remaining <= 0) continue;
            // Extra deadline prevents stale highlights if updates stall; effect expiration is independent.
            targets.Add(new(actor, now + Math.Min(remaining, Math.Clamp(intervalMs, 50, 500) / 1000d + 0.05)));
            if (targets.Count == MaxTargets) break;
        }
        LastScanMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }
}
