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
                confirmationReader??=new(interop,log) { SamplePosition = SampleImpactPosition };
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
    private Vector3? SampleImpactPosition(ulong targetId)
    {
        // The selected enemy can disappear during the original action-effect handler.
        // Only touch Dalamud objects on the framework thread, and copy a value before forwarding.
        if (!framework.IsInFrameworkUpdateThread) return null;
        var actor = targets.Target;
        if (actor == null || !actor.IsValid() || actor.GameObjectId != targetId) return null;
        var position = actor.Position;
        return float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) ? position : null;
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
                    if (now-effect.At >= 3) continue;
                    var target=commandVisuals.Confirm(effect.Owner,effect.Action,effect.Type,effect.Target,effect.Sequence,effect.At);
                    if (target is not {} confirmed) continue;
                    if (effect.TargetPosition is {} impact)
                        commandVisuals.ShowRing(impact,now,S.ConfirmedRingSeconds,confirmed);
                    else if (TryCommandPosition(confirmed,out var center))
                        commandVisuals.ShowRing(center,now,S.ConfirmedRingSeconds,confirmed);
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
            var viewport = ImGui.GetMainViewport();
            var draw=ImGui.GetForegroundDrawList(viewport);
            if(S.CommandMarker&&now<commandVisuals.MarkerUntil&&commandVisuals.Selected is {} target)
            {
                var location = TryCommandPosition(target,out var live) ? live : commandVisuals.MarkerImpact;
                if (location is {} position && TryMarkerTip(position,viewport,out var head))
                    DrawMarker(draw,head,S.CommandMarkerSize,ImGui.ColorConvertFloat4ToU32(S.CommandMarkerColor),S.CommandMarkerColor.W);
            }
            if(S.ConfirmedRing&&now<commandVisuals.RingUntil&&commandVisuals.RingCenter is {} center)
                DrawWorldRing(draw,center+Vector3.UnitY*S.ConfirmedRingHeight,Zantetsuken.Radius,
                    ImGui.ColorConvertFloat4ToU32(S.ConfirmedRingColor),S.ConfirmedRingWidth);
        }
        catch(Exception ex){ClearCommandVisuals();log.Warning(ex,"Local Zantetsuken drawing stopped.");}
    }
    private bool TryMarkerTip(Vector3 position, ImGuiViewportPtr viewport, out Vector2 tip)
    {
        var inFront = gui.WorldToScreen(position + Vector3.UnitY * S.CommandMarkerHeight, out tip, out var inView);
        if (!inFront || !float.IsFinite(tip.X) || !float.IsFinite(tip.Y) || !inView)
        {
            // Keep the marker with a visible enemy even when its configured height projects off-screen.
            if (!Project(position, out var feet)) return false;
            if (!inFront || !float.IsFinite(tip.X) || !float.IsFinite(tip.Y)) tip = feet;
        }
        var size = S.CommandMarkerSize;
        var minimum = viewport.Pos + new Vector2(size + 1, size * 1.5f + 1);
        var maximum = viewport.Pos + viewport.Size - new Vector2(size + 1, 1);
        if (maximum.X < minimum.X || maximum.Y < minimum.Y) return false;
        tip = Vector2.Clamp(tip, minimum, maximum);
        return true;
    }
}
