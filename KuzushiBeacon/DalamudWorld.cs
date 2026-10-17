using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace KuzushiBeacon;

internal sealed class DalamudWorld(IObjectTable objects) : IHighlightWorld
{
    public IEnumerable<Actor> Players
    {
        get
        {
            foreach (var player in objects.PlayerObjects)
            {
                if (player is not IPlayerCharacter || !player.IsValid()) continue;
                yield return new Actor(player.ObjectIndex, player.EntityId, player.GameObjectId,
                    player.Position, !player.IsDead && player.CurrentHp > 0, player.IsTargetable);
            }
        }
    }

    public float OwnKuzushiRemaining(int index, uint sourceId)
    {
        if (objects[index] is not IBattleChara actor) return 0;
        float remaining = 0;
        foreach (var status in actor.StatusList)
            if (Rules.OwnStatus(status.StatusId, status.SourceId, sourceId, status.RemainingTime))
                remaining = Math.Max(remaining, status.RemainingTime);
        return remaining;
    }
}
