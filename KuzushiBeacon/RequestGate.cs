namespace KuzushiBeacon;

// Framework-thread state. Ambiguous requests remain latched; never resend on a timer.
public sealed class RequestGate
{
    public bool Pending { get; private set; }
    private double retryAfter;
    private double nextMessage;
    public bool Begin(double now)
    {
        if (Pending || now < retryAfter) return false;
        Pending = true;
        return true;
    }
    public void Rejected(double now) { Pending = false; retryAfter = now + 1; }
    public void Observe(bool fullGauge) { if (!fullGauge) Pending = false; }
    public bool Message(double now)
    {
        if (now < nextMessage) return false;
        nextMessage = now + 3;
        return true;
    }
    public void Reset() { Pending = false; retryAfter = 0; }
}
