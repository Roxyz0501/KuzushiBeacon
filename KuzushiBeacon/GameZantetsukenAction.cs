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
        var actor = Resolve(candidate);
        var player = objects.LocalPlayer;
        return actor is not null && player is not null && player.IsValid()
            && ActionManager.CanUseActionOnTarget(Zantetsuken.ActionId, (NativeObject*)actor.Address)
            && ActionManager.GetActionInRangeOrLoS(Zantetsuken.ActionId, (NativeObject*)player.Address, (NativeObject*)actor.Address) == 0;
    }

    public bool CanUse(Actor candidate)
    {
        var actor = Resolve(candidate);
        var manager = ActionManager.Instance();
        var player = objects.LocalPlayer;
        return actor is not null && player is not null && player.IsValid() && manager != null
            && !(manager->ActionQueued && manager->QueuedActionId == Zantetsuken.ActionId)
            && ActionManager.CanUseActionOnTarget(Zantetsuken.ActionId, (NativeObject*)actor.Address)
            && ActionManager.GetActionInRangeOrLoS(Zantetsuken.ActionId, (NativeObject*)player.Address, (NativeObject*)actor.Address) == 0
            && manager->GetActionStatus(ActionType.Action, Zantetsuken.ActionId, actor.GameObjectId) == 0;
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
        if (!Gate.Begin(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency)) return false;
        targets.Target = actor;
        // Same normal execution mode as a hotbar button press; client handles queueing/checks.
        var manager = ActionManager.Instance();
        var sequence = manager->LastUsedActionSequence;
        var accepted = manager->UseAction(ActionType.Action, Zantetsuken.ActionId,
            actor.GameObjectId, mode: ActionManager.UseActionMode.None);
        // A queued request or changed sequence also counts as issued, even if return is false.
        if (!accepted && !manager->ActionQueued && manager->LastUsedActionSequence == sequence)
            Gate.Rejected(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
        return accepted || Gate.Pending;
    }
}
