using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace KuzushiBeacon;

// Uses the client's normal action entry point; no packets, hooks or state-field writes.
internal sealed unsafe class GameZantetsukenAction(IClientState client, IObjectTable objects,
    ITargetManager targets, IHighlightWorld world) : IZantetsukenAction
{
    private IGameObject? Resolve(Actor candidate)
    {
        var actor = objects[candidate.Index];
        return actor is not null && actor.IsValid() && actor.EntityId == candidate.EntityId
            && actor.GameObjectId == candidate.ObjectId && !actor.IsDead && actor.IsTargetable ? actor : null;
    }

    public bool CanUse(Actor candidate)
    {
        var actor = Resolve(candidate);
        var manager = ActionManager.Instance();
        var player = objects.LocalPlayer;
        return actor is not null && player is not null && player.IsValid() && manager != null
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
        targets.Target = actor;
        // Macro mode does not queue a later action. Normal client checks remain in effect.
        return ActionManager.Instance()->UseAction(ActionType.Action, Zantetsuken.ActionId,
            actor.GameObjectId, mode: ActionManager.UseActionMode.Macro);
    }
}
