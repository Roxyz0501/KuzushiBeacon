using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace KuzushiBeacon;

internal readonly record struct ActionConfirmation(uint Owner,uint Action,byte Type,ulong Target,ushort Sequence,double At);

// Observe the existing receive entry point. Never alter arguments, packets or game state.
internal sealed unsafe class ActionConfirmationReader(IGameInteropProvider interop,IPluginLog log) : IDisposable
{
    private delegate void Receive(uint caster,Character* character,Vector3* position,ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,GameObjectId* targets);
    private Hook<Receive>? hook;
    private readonly ConcurrentQueue<ActionConfirmation> received=new();
    private int queued;
    private volatile uint owner;
    public bool Unavailable { get; private set; }
    public bool Enable(uint localOwner)
    {
        if(Unavailable)return false;
        try
        {
            if(hook==null)
            {
                var address=(nint)ActionEffectHandler.MemberFunctionPointers.Receive;
                if(address==0)throw new InvalidOperationException("Action-effect receive entry point is unavailable.");
                hook=interop.HookFromAddress<Receive>(address,OnReceive);
            }
            owner=localOwner;
            if(!hook.IsEnabled)hook.Enable();
            return true;
        }
        catch(Exception ex)
        {
            owner=0;Unavailable=true;log.Warning(ex,"Zantetsuken confirmation display unavailable; LB input is unaffected.");
            return false;
        }
    }
    private void OnReceive(uint caster,Character* character,Vector3* position,ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,GameObjectId* targets)
    {
        try
        {
            if(owner!=0&&caster==owner&&header!=null&&header->ActionId==Zantetsuken.ActionId&&header->ActionType==1)
            {
                if(Interlocked.Increment(ref queued)<=16)
                    received.Enqueue(new(caster,header->ActionId,header->ActionType,header->AnimationTargetId.Id,
                        header->SourceSequence,System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency));
                else Interlocked.Decrement(ref queued);
            }
        }
        catch { /* No managed observer exception may escape into the game. */ }
        finally { hook!.Original(caster,character,position,header,effects,targets); }
    }
    public bool TryRead(out ActionConfirmation confirmation)
    {
        if(!received.TryDequeue(out confirmation))return false;
        Interlocked.Decrement(ref queued);return true;
    }
    public void Disable()
    {
        owner=0;if(hook?.IsEnabled==true)hook.Disable();
        while(TryRead(out _)){}
    }
    public void Dispose(){Disable();hook?.Dispose();hook=null;}
}
