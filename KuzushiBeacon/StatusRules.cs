using Dalamud.Game.ClientState.Objects.Types;

namespace KuzushiBeacon;

internal static class StatusRules
{
    // Explicit local-sheet IDs; unknown effects are not claimed to be immunity.
    public static bool Guard(uint id) => id is 3054 or 3673;
    public static bool Invulnerable(uint id) => id is 82 or 1302 or 1836 or 3039 or 325 or 394 or 529 or 656 or 671 or 775 or 776 or 895 or 969 or 981 or 1570 or 1697 or 1829 or 4410 or 4539 or 4875;
    public static bool Purifiable(uint id) => id is 1343 or 1344 or 1345 or 1347 or 3085 or 3134 or 3167 or 3219;
    public static bool Control(uint id) => Purifiable(id) || id is 1348 or 3022 or 3174;
    public static string Label(uint id,bool japanese) => id switch
    {
        1343=>japanese?"スタン":"Stun",1344 or 3167=>japanese?"ヘヴィ":"Heavy",1345 or 3134=>japanese?"バインド":"Bind",
        1347=>japanese?"沈黙":"Silence",1348=>japanese?"睡眠":"Sleep",3022=>japanese?"半睡眠":"Half-asleep",
        3219=>japanese?"氷結":"Deep Freeze",3085=>japanese?"変身":"Miracle of Nature",3174=>japanese?"メテオドライヴ":"Meteodrive",_=>id.ToString()
    };
    public static bool Excluded(IBattleChara actor, bool guard, bool invulnerable)
    {
        if (!guard && !invulnerable) return false;
        foreach (var s in actor.StatusList)
            if (s.RemainingTime > 0 && (guard && Guard(s.StatusId) || invulnerable && Invulnerable(s.StatusId))) return true;
        return false;
    }
}
