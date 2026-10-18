using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace KuzushiBeacon;

internal sealed class GameLowHpTarget(IClientState client, IObjectTable objects, ITargetManager targets)
{
    private static unsafe bool Enemy(IPlayerCharacter player)
        => ActionManager.ClassifyTarget((NativeCharacter*)player.Address) == ActionManager.TargetCategory.Enemy;

    public bool Select(float range)
    {
        if (!client.IsLoggedIn || !client.IsPvPExcludingDen) return false;
        var player = objects.LocalPlayer;
        if (player is null || !player.IsValid() || player.IsDead) return false;
        var owner = player.EntityId;
        var origin = player.Position;
        var ownRadius = player.HitboxRadius;
        var candidates = new List<HpCandidate>();
        int visited = 0;
        foreach (var entry in objects.PlayerObjects)
        {
            if (visited++ >= Tracker.MaxCandidates) break;
            if (entry is not IPlayerCharacter enemy || !enemy.IsValid() || enemy.IsDead || !enemy.IsTargetable
                || enemy.EntityId == owner || enemy.CurrentHp == 0
                || !LowHpTarget.InRange(origin, ownRadius, enemy.Position, enemy.HitboxRadius, range)) continue;
            candidates.Add(new(new Actor(enemy.ObjectIndex, enemy.EntityId, enemy.GameObjectId,
                enemy.Position, true, true), enemy.CurrentHp, enemy.HitboxRadius, Enemy(enemy)));
        }
        var selected = LowHpTarget.Find(candidates, owner, origin, ownRadius, range);
        if (selected is not { } best) return false;
        if (objects[best.Actor.Index] is not IPlayerCharacter target || !target.IsValid()
            || target.EntityId != best.Actor.EntityId || target.GameObjectId != best.Actor.ObjectId
            || target.IsDead || target.CurrentHp == 0 || !target.IsTargetable || !Enemy(target)
            || !LowHpTarget.InRange(player.Position, player.HitboxRadius, target.Position, target.HitboxRadius, range)) return false;
        if (targets.Target?.GameObjectId != target.GameObjectId) targets.Target = target;
        return true;
    }
}
