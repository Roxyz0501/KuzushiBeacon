using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace KuzushiBeacon;

internal sealed class GameLowHpTarget(IClientState client, IObjectTable objects, ITargetManager targets)
{
    internal static unsafe bool Enemy(IPlayerCharacter player)
        => ActionManager.ClassifyTarget((NativeCharacter*)player.Address) == ActionManager.TargetCategory.Enemy;
    internal static unsafe bool Ally(IPlayerCharacter player)
        => ActionManager.ClassifyTarget((NativeCharacter*)player.Address) is ActionManager.TargetCategory.Party or ActionManager.TargetCategory.Alliance;
    public string LastReason { get; private set; } = "";
    public IPlayerCharacter? LastSelection { get; private set; }
    public bool Select(float range, SupportOptions? options = null, Func<Vector3,bool>? visible = null, bool nearest = false, bool ally = false)
    {
        LastSelection = null; LastReason = "conditions";
        if (!client.IsLoggedIn || !client.IsPvPExcludingDen) return false;
        var player = objects.LocalPlayer;
        if (player is null || !player.IsValid() || player.IsDead) return false;
        IPlayerCharacter? best = null;
        var distance = float.MaxValue;
        int visited = 0; bool saw = false, inRange = false;
        foreach (var entry in objects.PlayerObjects)
        {
            if (visited++ >= Tracker.MaxCandidates) break;
            if (entry is not IPlayerCharacter p || !p.IsValid() || p.IsDead || !p.IsTargetable || p.EntityId == player.EntityId
                || p.CurrentHp == 0 || !(ally ? Ally(p) : Enemy(p))) continue;
            saw = true;
            if (!LowHpTarget.InRange(player.Position, player.HitboxRadius, p.Position, p.HitboxRadius, range)) continue;
            inRange = true;
            if (!Eligible(p, options, visible, ally)) continue;
            var d = Math.Max(0, Vector3.Distance(player.Position,p.Position)-player.HitboxRadius-p.HitboxRadius);
            if (best == null || (nearest ? d < distance : p.CurrentHp < best.CurrentHp)
                || (nearest ? d == distance : p.CurrentHp == best.CurrentHp) && (d < distance || d == distance && p.EntityId < best.EntityId))
            { best=p; distance=d; }
        }
        LastReason = !saw ? "no actors" : !inRange ? "out of range" : "filtered";
        if (best == null || !best.IsValid() || best.IsDead || !best.IsTargetable || best.CurrentHp == 0
            || !(ally?Ally(best):Enemy(best)) || !Eligible(best,options,visible,ally)
            || !LowHpTarget.InRange(player.Position,player.HitboxRadius,best.Position,best.HitboxRadius,range)) return false;
        LastSelection=best; LastReason=nearest?"nearest":"lowest HP";
        if (options?.On(99) != true && targets.Target?.GameObjectId != best.GameObjectId) targets.Target=best;
        return true;
    }
    private static bool Eligible(IPlayerCharacter p, SupportOptions? o, Func<Vector3,bool>? visible, bool ally)
        => o == null || (ally || !o.On(4) || visible?.Invoke(p.Position + Vector3.UnitY)==true)
        && (ally || !StatusRules.Excluded(p,o.ExcludeGuard,o.ExcludeInvulnerable));
}
