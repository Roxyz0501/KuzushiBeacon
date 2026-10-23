namespace KuzushiBeacon;

// A single continuation of an explicit command, never a retry loop.
internal sealed class PurifyFollowup
{
    public bool Pending { get; private set; }
    public uint Owner { get; private set; }
    public ulong Target { get; private set; }
    private double expires;
    public static bool BlockingStatus(uint id) => id is 1343 or 1345 or 1347 or 3085 or 3134 or 3219;
    public void Begin(double now, uint owner, ulong target)
    {
        Pending = true; Owner = owner; Target = target; expires = now + 2;
    }
    public void Cancel() { Pending = false; Owner = 0; Target = 0; expires = 0; }
    public bool Take(double now, bool contextValid, bool blocked, bool ready)
    {
        if (!Pending) return false;
        if (!contextValid || now >= expires) { Cancel(); return false; }
        if (blocked || !ready) return false;
        Cancel();
        return true;
    }
}
