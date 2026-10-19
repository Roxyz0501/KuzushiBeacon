namespace KuzushiBeacon;

// Framework-thread state. Observation never executes an action; recovery requires a new command.
public sealed class RequestGate
{
    public bool Pending { get; private set; }
    private double retryAfter;
    private double nextMessage;
    private double pendingSince;
    private ushort lastSequence;
    private bool queuedOnly;
    public bool Begin(double now)
    {
        if (Pending || now < retryAfter) return false;
        Pending = true;
        pendingSince = now;
        queuedOnly = false;
        return true;
    }
    public void Result(double now, bool accepted, bool ownActionQueued, ushort before, ushort after)
    {
        lastSequence = after;
        queuedOnly = ownActionQueued && before == after;
        if (!accepted && !ownActionQueued && before == after) Rejected(now);
    }
    public void Rejected(double now) { Pending = false; queuedOnly = false; retryAfter = now + 1; }
    public void Observe(bool fullGauge) { if (!fullGauge) { Pending = false; queuedOnly = false; } }
    public void Observe(double now, bool fullGauge, bool ownActionQueued, ushort sequence, bool idle, bool explicitPress)
    {
        Observe(fullGauge);
        if (!Pending) return;
        // A queue may have executed between observations. Any sequence change is treated conservatively.
        if (queuedOnly && sequence != lastSequence) { lastSequence = sequence; queuedOnly = false; pendingSince = now; }
        if (ownActionQueued) { pendingSince = now; return; }
        if (queuedOnly) { Rejected(now); return; } // Queue disappeared without an issued action.
        // Unknown outcome is not proof of rejection. Allow a fresh, validated manual attempt only.
        if (explicitPress && idle && now - pendingSince >= 3) { Pending = false; queuedOnly = false; }
    }
    public bool Message(double now)
    {
        if (now < nextMessage) return false;
        nextMessage = now + 3;
        return true;
    }
    public void Reset() { Pending = false; queuedOnly = false; retryAfter = 0; }
}
