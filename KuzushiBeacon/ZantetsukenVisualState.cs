using System.Numerics;

namespace KuzushiBeacon;

// Values only. A received action effect, not a successful UseAction or a spent gauge, confirms a ring.
public sealed class ZantetsukenVisualState
{
    private sealed record Attempt(uint Owner, Actor Target, ushort? Sequence, double From, double Until);
    private readonly List<Attempt> attempts=[];
    public Actor? Selected { get; private set; }
    public double MarkerUntil { get; private set; }
    public Vector3? MarkerImpact { get; private set; }
    public Vector3? RingCenter { get; private set; }
    public double RingUntil { get; private set; }
    public bool Waiting => attempts.Count>0;
    public int PendingCount => attempts.Count;
    public bool Visible => Selected!=null||RingCenter!=null;
    public void Select(Actor target,double now,float seconds)
    {Selected=seconds>0?target:null;MarkerUntil=now+seconds;MarkerImpact=null;}
    public void Request(uint owner,Actor target,ushort? sequence,double now)
    {
        Expire(now);
        if(!Rules.ValidId(owner))return;
        // Repeated queued presses for the same target share a pending confirmation.
        var index = attempts.FindIndex(a=>a.Owner==owner&&a.Target.ObjectId==target.ObjectId&&a.Sequence==sequence);
        if (index >= 0)
        {
            attempts[index] = attempts[index] with { Until = now + 3 };
            return;
        }
        if(attempts.Count==16)attempts.RemoveAt(0);
        attempts.Add(new(owner,target,sequence,now,now+3));
    }
    public Actor? Confirm(uint owner,uint action,byte type,ulong target,ushort sequence,double receivedAt)
    {
        if(action!=Zantetsuken.ActionId||type!=1)return null;
        var match=attempts.FirstOrDefault(a=>a.Owner==owner&&a.Target.ObjectId==target&&receivedAt<a.Until
            &&receivedAt>=a.From&&(a.Sequence==null||a.Sequence==sequence));
        if(match==null)return null;
        attempts.Clear(); // One LB consumes the opportunity. Duplicate effects cannot restart the ring.
        return match.Target;
    }
    public void ShowRing(Vector3 center,double now,float seconds,Actor? confirmedTarget=null)
    {
        if(!float.IsFinite(center.X)||!float.IsFinite(center.Y)||!float.IsFinite(center.Z))return;
        RingCenter=center;RingUntil=now+seconds;
        if (Selected is {} selected && confirmedTarget?.ObjectId == selected.ObjectId)
            MarkerImpact = center;
    }
    public void Expire(double now)
    {
        attempts.RemoveAll(a=>now>=a.Until);
        if(now>=MarkerUntil)Selected=null;
        if(now>=RingUntil)RingCenter=null;
    }
    public void HideMarker()=>Selected=null;
    public void HideRing(){RingCenter=null;attempts.Clear();}
    public void Clear(){Selected=null;RingCenter=null;attempts.Clear();MarkerUntil=RingUntil=0;}
}
