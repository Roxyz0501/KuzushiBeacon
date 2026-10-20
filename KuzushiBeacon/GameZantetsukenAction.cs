using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace KuzushiBeacon;

// Uses the client's normal action entry point; no packets, hooks or state-field writes.
internal sealed unsafe class GameZantetsukenAction(IClientState client, IObjectTable objects,
    ITargetManager targets, IHighlightWorld world) : IZantetsukenAction
{
    public RequestGate Gate { get; } = new();
    public SupportOptions? Options { get; set; }
    public bool TargetOnly { get; set; }
    public Actor? LastSelection { get; private set; }
    public bool Included(Actor candidate) => Resolve(candidate) is IBattleChara actor
        && !StatusRules.Excluded(actor, Options?.ZantetsukenExcludeGuard == true, Options?.ZantetsukenExcludeInvulnerable == true);

    private IGameObject? Resolve(Actor candidate)
    {
        var actor = objects[candidate.Index];
        return actor is not null && actor.IsValid() && actor.EntityId == candidate.EntityId
            && actor.GameObjectId == candidate.ObjectId && !actor.IsDead && actor.IsTargetable ? actor : null;
    }

    public static bool IsLimitBreakFull() => TryReadGauge(out var full) && full;

    public static bool TryReadGauge(out bool full)
    {
        var gauge = LimitBreakController.Instance();
        full = false;
        if (gauge == null || !gauge->IsPvP || gauge->BarUnits == 0 || gauge->BarCount == 0) return false;
        full = Zantetsuken.FullGauge(true, gauge->CurrentUnits, gauge->BarUnits, gauge->BarCount);
        return true;
    }

    // Display depends on gauge fullness and reach, not temporary cast/animation locks.
    public bool CanReach(Actor candidate)
    {
        if (!Included(candidate)) return false;
        var actor = Resolve(candidate);
        var player = objects.LocalPlayer;
        return actor is not null && player is not null && player.IsValid()
            && ActionManager.CanUseActionOnTarget(Zantetsuken.ActionId, (NativeObject*)actor.Address)
            && ActionManager.GetActionInRangeOrLoS(Zantetsuken.ActionId, (NativeObject*)player.Address, (NativeObject*)actor.Address) == 0;
    }

    // Candidate eligibility only. Native UseAction owns readiness and queue handling.
    public bool CanUse(Actor candidate) => CanReach(candidate);

    private static bool OwnActionQueued(ActionManager* manager) => manager->ActionQueued
        && manager->QueuedActionType == ActionType.Action && manager->QueuedActionId == Zantetsuken.ActionId;

    public void ObserveRequest(double now, bool fullGauge, bool explicitPress = false)
    {
        Gate.Observe(fullGauge);
        if (!Gate.Pending) return;
        var manager = ActionManager.Instance();
        if (manager == null) return; // Unknown native state cannot authorize another request.
        Gate.Observe(now, fullGauge, OwnActionQueued(manager), manager->LastUsedActionSequence,
            !manager->ActionQueued && manager->CastTimeTotal <= 0 && manager->AnimationLock <= 0, explicitPress);
    }

    public bool TryExecute(Actor candidate, uint owner)
    {
        if (!client.IsLoggedIn || !client.IsPvPExcludingDen) return false;
        var player = objects.LocalPlayer;
        if (player is null || !player.IsValid() || player.IsDead || player.EntityId != owner
            || player.ClassJob.RowId != Rules.SamuraiJob) return false;
        if (world.OwnKuzushiRemaining(candidate.Index, owner) <= 0 || !CanUse(candidate)) return false;
        var actor = Resolve(candidate);
        if (actor is null) return false;
        LastSelection = candidate;
        if (Options?.On(99) == true) return true;
        if (TargetOnly) { targets.Target = actor; return true; }
        // Diagnostic state only: a previous request must never suppress a new explicit press.
        var manager = ActionManager.Instance();
        if (manager == null) return false;
        Gate.Reset();
        Gate.Begin(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
        targets.Target = actor;
        // Same normal execution mode as a hotbar button press; client handles queueing/checks.
        var sequence = manager->LastUsedActionSequence;
        var accepted = manager->UseAction(ActionType.Action, Zantetsuken.ActionId,
            actor.GameObjectId, mode: ActionManager.UseActionMode.None);
        Gate.Result(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency,
            accepted, OwnActionQueued(manager), sequence, manager->LastUsedActionSequence);
        return accepted || Gate.Pending;
    }
}
