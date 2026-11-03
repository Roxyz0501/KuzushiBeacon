using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace KuzushiBeacon;

public sealed partial class Plugin
{
    private readonly ZantetsukenVisualState commandVisuals=new();
    private ActionConfirmationReader? confirmationReader;
    private double commandPreparedAt;
    private bool commandVisualTick,commandVisualDraw,confirmationWarning;
    private bool CommandVisualsAllowed => !disposed&&config.Enabled&&S.ZantetsukenSupport
        &&!S.DisabledGroups.Contains(2)&&client.IsLoggedIn&&client.IsPvPExcludingDen&&!On(99);

    private void CommandTargetSelected(Actor target,uint owner,bool execute)
    {
        try
        {
            if(!CommandVisualsAllowed)return;
            commandPreparedAt=Now;
            commandVisuals.Select(target,commandPreparedAt,S.CommandMarker?S.CommandMarkerSeconds:0);
            if(execute&&S.ConfirmedRing)
            {
                confirmationReader??=new(interop,log);
                if(!confirmationReader.Enable(owner)&&!confirmationWarning)
                {confirmationWarning=true;chat.Print("PvP Supporter: "+T("CommandRingUnavailable"));}
            }
            SyncCommandVisualCallbacks();
        }
        catch(Exception ex){log.Warning(ex,"Could not prepare local Zantetsuken feedback.");}
    }
    private void CommandActionRequested(Actor target,uint owner,ushort? sequence)
    {
        try
        {
            if(!CommandVisualsAllowed||!S.ConfirmedRing||confirmationReader?.Unavailable!=false)return;
            commandVisuals.Request(owner,target,sequence,commandPreparedAt);SyncCommandVisualCallbacks();
        }
        catch(Exception ex){log.Warning(ex,"Could not track local Zantetsuken confirmation.");}
    }
    private bool TryCommandPosition(Actor target,out Vector3 position)
    {
        var actor=objects[target.Index];
        if(actor!=null&&actor.IsValid()&&actor.EntityId==target.EntityId&&actor.GameObjectId==target.ObjectId)
        {position=actor.Position;return float.IsFinite(position.X)&&float.IsFinite(position.Y)&&float.IsFinite(position.Z);}
        position=default;return false;
    }
    private void UpdateCommandVisuals(IFramework _)
    {
        try
        {
            if(!CommandVisualsAllowed){ClearCommandVisuals();return;}
            var now=Now;
            if(!S.CommandMarker)commandVisuals.HideMarker();
            if(!S.ConfirmedRing)commandVisuals.HideRing();
            if(confirmationReader!=null)
                while(confirmationReader.TryRead(out var effect))
                {
                    if(now-effect.At>.5)continue;
                    var target=commandVisuals.Confirm(effect.Owner,effect.Action,effect.Type,effect.Target,effect.Sequence,effect.At);
                    if(target is {} confirmed&&TryCommandPosition(confirmed,out var center))
                        commandVisuals.ShowRing(center,now,S.ConfirmedRingSeconds);
                }
            commandVisuals.Expire(now);
            if(!commandVisuals.Waiting)confirmationReader?.Disable();
            SyncCommandVisualCallbacks();
        }
        catch(Exception ex){ClearCommandVisuals();log.Warning(ex,"Local Zantetsuken feedback stopped.");}
    }
    private void SyncCommandVisualCallbacks()
    {
        var tick=commandVisuals.Visible||commandVisuals.Waiting;
        if(tick!=commandVisualTick)
        {if(tick)framework.Update+=UpdateCommandVisuals;else framework.Update-=UpdateCommandVisuals;commandVisualTick=tick;}
        var draw=commandVisuals.Visible;
        if(draw!=commandVisualDraw)
        {if(draw)pi.UiBuilder.Draw+=DrawCommandVisuals;else pi.UiBuilder.Draw-=DrawCommandVisuals;commandVisualDraw=draw;}
    }
    private void ClearCommandVisuals()
    {commandVisuals.Clear();confirmationReader?.Disable();SyncCommandVisualCallbacks();}
    private void DrawCommandVisuals()
    {
        try
        {
            if(!CommandVisualsAllowed)return;
            var now=Now;
            var draw=ImGui.GetForegroundDrawList();
            if(S.CommandMarker&&now<commandVisuals.MarkerUntil&&commandVisuals.Selected is {} target
                &&TryCommandPosition(target,out var position)&&Project(position+Vector3.UnitY*S.CommandMarkerHeight,out var head))
                DrawMarker(draw,head,S.CommandMarkerSize,ImGui.ColorConvertFloat4ToU32(S.CommandMarkerColor),S.CommandMarkerColor.W);
            if(S.ConfirmedRing&&now<commandVisuals.RingUntil&&commandVisuals.RingCenter is {} center)
                DrawWorldRing(draw,center+Vector3.UnitY*S.ConfirmedRingHeight,Zantetsuken.Radius,
                    ImGui.ColorConvertFloat4ToU32(S.ConfirmedRingColor),S.ConfirmedRingWidth);
        }
        catch(Exception ex){ClearCommandVisuals();log.Warning(ex,"Local Zantetsuken drawing stopped.");}
    }
}
