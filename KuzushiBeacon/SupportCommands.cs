using System.Globalization;
using System.Text.Json;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace KuzushiBeacon;

public sealed partial class Plugin
{
    private readonly IPlayerState playerState;
    private readonly IDutyState duty;
    private readonly IDataManager data;
    private readonly IGameInteropProvider interop;
    private Ledger.LedgerModule? ledger;
    private readonly List<string> supportCommands=[];
    private bool reviewLoaded;
    private void RegisterSupportCommands()
    {
        foreach(var name in new[]{"/pvplowhp3","/pvplowhp5","/pvplowhp20","/pvpnearest","/pvpally","/pvptrack","/pvpassist","/pvpztarget","/pvpmute"})
        {commands.AddHandler(name,new CommandInfo((cmd,args)=>_=SelectSupportTargetAsync(cmd,args)){HelpMessage="PvP Supporter: "+name});supportCommands.Add(name);}
    }
    private void ReconcileLedger()
    {
        if(disposed)return;
        if(config.Enabled&&S.LedgerEnabled&&ledger==null)
            ledger=new Ledger.LedgerModule(pi,commands,client,playerState,duty,data,framework,chat,interop,log,config.Ledger,()=>{dirty=true;Save();});
        else if((!config.Enabled||!S.LedgerEnabled)&&ledger!=null){ledger.Dispose();ledger=null;}
        largeContent=false;
        if(client.IsPvPExcludingDen)
        {
            try
            {
                var cfc=duty.ContentFinderCondition.Value;
                var type=data.GetExcelSheet<Lumina.Excel.Sheets.InstanceContent>().GetRow(cfc.Content.RowId).InstanceContentType.RowId;
                largeContent=type is 6 or 12;
            }
            catch { /* Unknown mode uses the small/default profile. */ }
        }
        S.CurrentLarge=largeContent;
        if(On(86)&&client.IsPvPExcludingDen&&duty.IsDutyStarted&&!reviewCompleted&&reviewStarted==null)reviewStarted=DateTimeOffset.UtcNow;
        if(!reviewLoaded&&On(86))
        {
            reviewLoaded=true;var path=Path.Combine(pi.GetPluginConfigDirectory(),"support-review","matches.json");
            // A capped local file is loaded only on first opt-in, never per-frame or per-tab.
            try {if(File.Exists(path)&&new FileInfo(path).Length<=4*1024*1024)reviewHistory.AddRange((JsonSerializer.Deserialize<ReviewRecord[]>(File.ReadAllText(path))??[]).TakeLast(100));}
            catch(Exception ex){log.Warning(ex,"Could not load local support review.");}
        }
    }
    private async Task SelectSupportTargetAsync(string command,string args)
    {
        var requested=Now;
        try
        {
            await framework.RunOnFrameworkThread(()=>
            {
                if(disposed||Now-requested>.5||Now<nextLowHpCommand)return;
                nextLowHpCommand=Now+.25;
                if(command=="/pvpmute")
                {
                    if(!On(81))return;
                    var seconds=string.IsNullOrWhiteSpace(args)?60:double.TryParse(args,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&double.IsFinite(value)?Math.Clamp(value,0,3600):-1;
                    if(seconds>=0){muteUntil=Now+seconds;localSound.Stop();}return;
                }
                if(!config.Enabled||!client.IsLoggedIn||!client.IsPvPExcludingDen)return;
                var player=objects.LocalPlayer;
                if(player==null||!player.IsValid()||player.IsDead)return;
                if(command=="/pvpztarget") {if(string.IsNullOrWhiteSpace(args))_=ExecuteZantetsukenAsync(true);return;}
                if(command=="/pvptrack")
                {
                    if(args.Trim()=="clear"){trackedAlly=0;return;}
                    if(string.IsNullOrWhiteSpace(args)&&targets.Target is IPlayerCharacter ally&&ally.IsValid()&&GameLowHpTarget.Ally(ally))trackedAlly=ally.GameObjectId;
                    return;
                }
                if(command=="/pvpassist")
                {
                    if(!On(72)||!string.IsNullOrWhiteSpace(args))return;
                    var ally=objects.PlayerObjects.OfType<IPlayerCharacter>().Take(Tracker.MaxCandidates).FirstOrDefault(a=>a.GameObjectId==trackedAlly&&a.IsValid()&&!a.IsDead&&GameLowHpTarget.Ally(a));
                    var enemy=ally==null?null:objects.PlayerObjects.OfType<IPlayerCharacter>().Take(Tracker.MaxCandidates).FirstOrDefault(a=>a.GameObjectId==ally.TargetObjectId&&a.IsValid()&&!a.IsDead&&a.IsTargetable&&GameLowHpTarget.Enemy(a));
                    var ok=enemy!=null&&!StatusRules.Excluded(enemy,S.ExcludeGuard,S.ExcludeInvulnerable)&&(!On(4)||OnScreen(enemy.Position));
                    if(ok&&!On(99))targets.Target=enemy;
                    CountCommand("assist",ok,"no observed ally target");return;
                }
                var friendly=command=="/pvpally";
                if(!friendly&&S.DisabledGroups.Contains(0))return;
                if(friendly&&!On(70))return;
                var range=friendly?S.AllyRange:SupportOptions.JobRange(player.ClassJob.RowId,S.MeleeRange,S.RangedRange);
                var fixedRange=command switch{"/pvplowhp3"=>3f,"/pvplowhp5"=>5f,"/pvplowhp20"=>20f,_=>0};
                if(fixedRange>0){if(!string.IsNullOrWhiteSpace(args))return;range=fixedRange;}
                else if(!string.IsNullOrWhiteSpace(args)&&args.Trim()!="auto")
                {
                    if(!float.TryParse(args,NumberStyles.Float,CultureInfo.InvariantCulture,out range)||!float.IsFinite(range)||range<1||range>50)
                    {if(messages.Message(Now))chat.Print(L("射程は1～50mで指定してください。","Specify a range between 1 and 50 metres."));return;}
                }
                var success=lowHpTarget.Select(range,S,OnScreen,command=="/pvpnearest",friendly);
                CountCommand(command,success,lowHpTarget.LastReason);
                if(!success&&messages.Message(Now))chat.Print(T("LowHpUnavailable"));
                else if(success&&On(99)&&messages.Message(Now))chat.Print(L("検証：候補HP ","Dry run: candidate HP ")+lowHpTarget.LastSelection?.CurrentHp+" · "+lowHpTarget.LastReason);
            });
        }
        catch(Exception ex){if(messages.Message(Now))log.Error(ex,"PvP Supporter explicit command failed; not retried.");}
    }
}
