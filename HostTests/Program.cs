using System.Reflection;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KuzushiBeacon;
using Lumina.Excel;
using ClassJob = Lumina.Excel.Sheets.ClassJob;

int checks = 0;
void Check(bool result, string description)
{
    if (!result) throw new Exception(description);
    checks++;
}

// Real plugin feedback lifecycle, with native receipt replaced only by a value-queue injection.
{
    var host=new Host{Pvp=true,FeedbackTargetPresent=true};
    host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;host.Config.Support.Features=[];
    using var plugin=host.Load();var pt=typeof(Plugin);
    object? Field(string name)=>pt.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin);
    void Call(string name,params object[] args)=>pt.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,args);
    double Now()=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    var state=(ZantetsukenVisualState)Field("commandVisuals")!;
    var target=new Actor(37,500,500,new(8,0,0),true,true);
    Check(host.Count("framework:Update")==0&&host.Count("ui:Draw")==0,"default command visuals do not add idle callbacks");
    var adapter=Field("zantetsukenAction")!;
    var selected=(Action<Actor,uint,bool>)adapter.GetType().GetProperty("TargetSelected")!.GetValue(adapter)!;
    selected(target,123,false);
    Check(state.Selected==target&&host.Count("ui:Draw")==1&&host.Count("framework:Update")==1,"actual selected-target callback starts temporary feedback independently of other overlays");
    Check(Field("confirmationReader")==null&&!state.Waiting,"selection-only command does not hook action confirmation");
    Call("Reconcile");
    Check(state.Selected==target && host.Count("ui:Draw")==1,"reconciling idle general overlays preserves temporary command feedback");
    var readerType=pt.Assembly.GetType("KuzushiBeacon.ActionConfirmationReader")!;
    var reader=Activator.CreateInstance(readerType,Field("interop"),Field("log"))!;
    pt.GetField("confirmationReader",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,reader);
    Call("CommandActionRequested",target,123u,(ushort)7);
    Check(state.Waiting&&state.RingCenter==null,"request acceptance alone has no confirmed ring");
    void Receive(uint owner,uint action,ulong targetId,ushort sequence,double? at=null,System.Numerics.Vector3? impact=null)
    {
        var effectType=pt.Assembly.GetType("KuzushiBeacon.ActionConfirmation")!;
        var effect=Activator.CreateInstance(effectType,owner,action,(byte)1,targetId,sequence,at??Now(),impact);
        var queue=readerType.GetField("received",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(reader)!;
        queue.GetType().GetMethod("Enqueue")!.Invoke(queue,[effect]);
        readerType.GetField("queued",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(reader,1);
        host.Raise("framework:Update",host.Framework);
    }
    Receive(999,29537,500,7);Check(state.RingCenter==null&&state.Waiting,"another player's result never displays own confirmation");
    Receive(123,29537,500,7);
    Check(state.RingCenter==host.FeedbackTargetPosition&&!state.Waiting,"received own action matches pending target and samples its current position");
    var expires=state.RingUntil;Receive(123,29537,500,7);Check(state.RingUntil==expires,"duplicate receipt cannot extend visual duration");
    Check(host.PlayerEnumerations==0,"feedback uses one object slot, never a player-table scan");
    state.Expire(double.MaxValue);host.Raise("framework:Update",host.Framework);
    Check(host.Count("ui:Draw")==0&&host.Count("framework:Update")==0,"expiry removes all temporary callbacks");
    selected(target,123,false);Call("CommandActionRequested",target,123u,(ushort)8);host.FeedbackTargetPresent=false;
    Receive(123,29537,500,8);Check(state.RingCenter==null&&!state.Waiting,"missing target does not produce a stale-position ring");
    selected(target,123,false);
    state.Request(123,target,9,Now()-1.2);
    Receive(123,29537,500,9,Now()-.8,host.FeedbackTargetPosition);
    Check(state.RingCenter==host.FeedbackTargetPosition && state.MarkerImpact==state.RingCenter,
        "an on-time receipt survives a delayed update and despawn, preserving the marked impact position");
    host.Pvp=false;host.Raise("client:LeavePvP");
    Check(!state.Visible&&!state.Waiting&&host.Count("ui:Draw")==0&&host.Count("framework:Update")==0,"PvP exit clears feedback immediately");
    selected(target,123,false);Check(!state.Visible,"outside-PvP selection cannot restart feedback");
}

// Idle workload audit: no actor scan for self-only displays; global notification switch stops work.
foreach(var feature in new[]{18})
{
    var host=new Host {Pvp=true};
    host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[feature,96];
    using var plugin=host.Load();host.Raise("framework:Update",host.Framework);
    Check(host.PlayerEnumerations==0,"self-only feature does not enumerate nearby actors: "+feature);
    Check(host.Errors==0,"self-only feature remains functional: "+feature);
}
foreach(var feature in new[]{24,33,35,39,40,46,47,48,57,58,59,63,65,68})
{
    var host=new Host {Pvp=true};host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[feature,96];host.Config.Support.Notifications=false;
    using var plugin=host.Load();
    for(int i=0;i<600;i++)host.Raise("framework:Update",host.Framework);
    Check(host.Count("framework:Update")==0&&host.Count("ui:Draw")==0&&host.ObjectReads==0,
        "muted notification feature has zero monitoring, draw callbacks and object reads: "+feature);
}

{
    var host=new Host {Pvp=true};host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[18,96];using var plugin=host.Load();
    host.Raise("framework:Update",host.Framework);
    typeof(Plugin).GetField("nextSupport",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,double.MaxValue);
    var reads=host.ObjectReads;for(int i=0;i<600;i++)host.Raise("framework:Update",host.Framework);
    Check(host.ObjectReads==reads&&host.PlayerEnumerations==0,"600 frames between support samples cause no player or actor reads");
}
foreach(var kind in new[]{"count","recommendation","kuzushi"})
{
    var host=new Host {Pvp=true};host.Config.ShowLine=host.Config.ShowMarker=kind=="kuzushi";host.Config.ShowMaxCount=kind=="count";
    host.Config.Support.Features=kind=="recommendation"?[41,96]:[96];host.Config.Support.KuzushiFullLbOnly=true;
    using var plugin=host.Load();host.Raise("framework:Update",host.Framework);
    Check(host.PlayerEnumerations==0&&host.Errors==0,"LB-gated display skips actors below full LB: "+kind);
    host.FullLb=true;
    typeof(Plugin).GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    host.Raise("framework:Update",host.Framework);
    Check(host.PlayerEnumerations==1&&host.Errors==0,"LB-gated display resumes when gauge fills: "+kind);
}
{
    var host=new Host {Pvp=true};host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[92,96];using var plugin=host.Load();
    Check(host.Count("framework:Update")==1&&host.Count("ui:Draw")==0,"record-only feature does not install any drawing callback");
    host.Raise("framework:Update",host.Framework);Check(host.PlayerEnumerations==1,"record-only feature retains required actor snapshot");
    host.Config.Support.Features=[56,96];host.Config.Support.FocusHud=host.Config.Support.FocusOverhead=false;
    typeof(Plugin).GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(host.Count("framework:Update")==0&&host.Count("ui:Draw")==0,"all output styles disabled stops the feature");
}
foreach(var pvp in new[]{false,true})
{
    var host=new Host {Pvp=pvp};host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[];host.Config.Support.LedgerEnabled=true;
    using var plugin=host.Load();host.Raise("framework:Update",host.Framework);
    Check(host.Errors==0&&host.Count("ui:Draw")==0&&host.Count("framework:Update")==0,
        "idle Ledger has no update or draw callbacks, even in PvP before duty: "+pvp);
    host.Config.Support.LedgerEnabled=false;
    typeof(Plugin).GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(host.Count("chat:LogMessage")==0,"disabling Ledger detaches chat handler");
}

{
    var host=new Host {Pvp=true};host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[];host.Config.Support.LedgerEnabled=true;
    using var plugin=host.Load();
    var module=typeof(Plugin).GetField("ledger",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
    var mt=module.GetType();void Set(string key,object? value)=>mt.GetField(key,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(module,value);
    void Call(string method,params object[] args)=>mt.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(module,args);
    Check(mt.GetField("fonts",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(module)==null,"idle Ledger does not build overlay fonts");
    Set("owner",KuzushiBeacon.Ledger.HistoryStore.OwnerKey(123));
    var session=new KuzushiBeacon.Ledger.MatchSession(DateTimeOffset.UtcNow,KuzushiBeacon.Ledger.Mode.CrystallineConflict,"test","test",KuzushiBeacon.Ledger.HistoryStore.OwnerKey(123),123,1,false);
    Set("current",session);Call("StartTicks");Call("ReconcileDraw");
    Check(host.Count("ui:Draw")==1,"active recorded match enables Ledger drawing");
    host.Config.Ledger.OverlayEnabled=false;Call("ChangeConfig");
    Check(host.Count("ui:Draw")==0,"overlay off detaches drawing while recording remains active");
    host.Config.Ledger.OverlayEnabled=true;Call("ChangeConfig");
    Check(host.Count("ui:Draw")==1,"overlay on restores recorded-match drawing");
    host.Pvp=false;host.Raise("client:LeavePvP");Call("FlushJobs");
    host.Raise("framework:Update",host.Framework);
    Check(host.Count("ui:Draw")==0&&host.Count("framework:Update")==0,"PvP exit drains record and stops Ledger ticks and draw");
    Check(Directory.EnumerateFiles(host.ConfigDirectory,"*.summary.json",SearchOption.AllDirectories).Count()==1,"idle optimization preserves final match save");
    Set("preview",true);mt.GetMethod("SetVisible")!.Invoke(module,[true]);host.Raise("framework:Update",host.Framework);
    Check(host.Count("ui:Draw")==1,"explicit preview still works outside PvP");
    mt.GetMethod("SetVisible")!.Invoke(module,[false]);mt.GetMethod("SetVisible")!.Invoke(module,[true]);
    Check(host.Count("ui:Draw")==1,"embedded settings visibility reset does not detach preview each frame");
    mt.GetMethod("SetVisible")!.Invoke(module,[false]);host.Raise("framework:Update",host.Framework);
    Check(host.Count("ui:Draw")==0&&host.Count("framework:Update")==0,"closing preview stops idle callbacks");
}

{
    var host=new Host {Pvp=true};host.Config.ShowMaxCount=false;host.Config.Support.Features=[18,96];
    using var plugin=host.Load();host.Raise("framework:Update",host.Framework);
    Check(host.GaugeReads==0,"own Kuzushi highlights and range ring do not read LB when no LB-dependent feature is enabled");
}

// Invoke the REAL plugin constructor on an installer-like worker thread.
foreach (var pvp in new[] { false, true })
{
    var host = new Host { Pvp = pvp };
    var plugin = Task.Run(host.Load).GetAwaiter().GetResult();
    Check(host.ObjectReads == 0, "constructor must not read ObjectTable");
    Check(host.Queue.Count == 1, "exactly one initial framework dispatch");
    host.Drain();
    Check(host.ObjectReads == 0, "PvP gate short circuits before ObjectTable");
    Check(host.Count("framework:Update") == (pvp ? 1 : 0), "monitor only subscribes inside PvP");
    Check(host.Count("ui:Draw") == (pvp ? 1 : 0), "overlay only subscribes inside PvP");
    host.Raise("client:Login");
    Check(host.Count("framework:Update") == (pvp ? 1 : 0), "repeat reconciliation never duplicates subscription");
    var reads = host.ObjectReads;
    for (var i = 0; i < 600; i++) if (!pvp) host.Raise("framework:Update", host.Framework);
    Check(host.ObjectReads == reads, "inactive framework ticks do no work");
    host.Pvp = false;
    host.Raise("client:LeavePvP");
    Check(host.Count("framework:Update") == 0 && host.Count("ui:Draw") == 0, "leave stops both subscriptions");
    plugin.Dispose();
    Check(host.TotalSubscriptions == 0, "dispose removes all callbacks");
    Check(host.Errors == 0 && host.IllegalReads == 0, "no lifecycle errors or off-thread reads");
}

{
    var host = new Host { Pvp = true };
    var plugin = Task.Run(host.Load).GetAwaiter().GetResult();
    plugin.Dispose();
    host.Drain();
    Check(host.ObjectReads == 0, "dispose before first tick cancels game access");
    Check(host.TotalSubscriptions == 0, "queued initialization cannot resurrect disposed plugin");
}
{
    var host = new Host { Pvp = true, ThrowOnRead = true };
    var plugin = Task.Run(host.Load).GetAwaiter().GetResult();
    host.Drain();
    host.Raise("framework:Update", host.Framework);
    SpinWait.SpinUntil(() => host.Errors == 1, 1000);
    Check(host.Errors == 1, "deferred failure is observed and logged");
    Check(host.Count("framework:Update") == 0 && host.Count("ui:Draw") == 0, "deferred failure stops monitor");
    plugin.Dispose();
    Check(host.TotalSubscriptions == 0, "failed deferred initialization stays disposable");
}
// Zone transitions can temporarily remove LocalPlayer without another client event.
{
    var host = new Host { Pvp = true, PlayerAvailable = false };
    using var plugin = host.Load();
    Check(host.Count("framework:Update") == 1, "PvP entry with missing player must keep recovery callback");
    host.Raise("framework:Update", host.Framework);
    host.PlayerAvailable = true;
    host.Raise("framework:Update", host.Framework);
    Check(host.ValidityReads == 1 && host.PlayerEnumerations == 1, "player returning without another event resumes detection");
    host.PlayerAvailable = false;
    host.Raise("framework:Update", host.Framework);
    host.PlayerAvailable = true;
    host.Raise("framework:Update", host.Framework);
    Check(host.ValidityReads == 2 && host.PlayerEnumerations == 2, "temporary player disappearance must not permanently stop detection");
    host.Job = 0;
    host.Raise("framework:Update", host.Framework);
    Check(host.PlayerEnumerations == 2, "unavailable job never scans enemies");
    host.Job = Rules.SamuraiJob;
    host.Raise("framework:Update", host.Framework);
    Check(host.PlayerEnumerations == 3, "job availability resumes scan without a new event");
    host.Pvp = false;
    host.Raise("client:LeavePvP");
    var reads = host.ObjectReads;
    for (var i = 0; i < 600; i++) host.Raise("framework:Update", host.Framework);
    Check(host.ObjectReads == reads && host.Count("framework:Update") == 0 && host.Count("ui:Draw") == 0,
        "recovery callback stops outside PvP with zero player reads");
}
{
    var host = new Host { Pvp = false };
    var plugin = host.Load();
    Check(host.Commands.ContainsKey("/kuzushizantetsuken"), "macro command registered");
    host.Commands["/kuzushizantetsuken"].Handler("/kuzushizantetsuken", "");
    Check(host.ObjectReads == 0, "macro outside PvP never accesses game objects");
    plugin.Dispose();
    Check(host.Commands.Count == 0, "dispose removes both command handlers");
}
{
    var host = new Host { Pvp = false };
    using var plugin = host.Load();
    for (int i = 0; i < 20; i++)
    {
        host.Commands["/kuzushizantetsuken"].Handler("/kuzushizantetsuken", "");
    }
    Check(host.ChatMessages == 1, "real command suppresses repeated unavailable messages");
    Check(host.ObjectReads == 0, "unavailable command spam never scans objects");
}
{
    var host = new Host { Pvp = true, Job = 0 };
    using var plugin = host.Load();
    var adapter = typeof(Plugin).GetField("zantetsukenAction", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(plugin)!;
    var observation = (RequestGate)adapter.GetType().GetProperty("Gate")!.GetValue(adapter)!;
    observation.Begin(0);
    var reads = host.ObjectReads;
    for (var press = 0; press < 20; press++)
        host.Commands["/kuzushizantetsuken"].Handler("/kuzushizantetsuken", "");
    Check(host.ObjectReads - reads == 20, "every immediate command revalidates player even with prior request pending");
    Check(host.ChatMessages == 1, "error text remains throttled independently from input");
    Check(host.Errors == 0, "repeat dispatch needs no native calls for invalid job");
}
{
    var host = new Host { Pvp = false };
    using var plugin = host.Load();
    Check(host.Commands.ContainsKey("/pvplowhp") && host.Commands.ContainsKey("/pvpsupporter")
        && host.Commands.ContainsKey("/kuzushibeacon"), "new commands and compatible settings alias registered");
    host.Commands["/pvplowhp"].Handler("/pvplowhp", "");
    Check(host.ObjectReads == 0 && host.PlayerEnumerations == 0, "low HP command outside PvP has no object reads");
}
{
    var host = new Host { Pvp = false };
    host.Config.Support.Features=FeatureCatalog.All.Select(f=>f.Id).ToHashSet();
    using var plugin=host.Load();
    foreach(var cmd in new[]{"/pvplowhp","/pvplowhp3","/pvplowhp5","/pvplowhp20","/pvpnearest","/pvpally","/pvptrack","/pvpassist","/pvpztarget"})
    {
        Check(host.Commands.ContainsKey(cmd),"new macro registered: "+cmd);
        typeof(Plugin).GetField("nextLowHpCommand",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,0d);
        host.Commands[cmd].Handler(cmd,"");
    }
    for(int i=0;i<600;i++)host.Raise("framework:Update",host.Framework);
    Check(host.ObjectReads==0&&host.PlayerEnumerations==0&&host.Errors==0,"all features and commands outside PvP perform zero object reads without hidden initialization errors");
}
{
    var host=new Host {Pvp=true};
    host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[83,96];
    using var plugin=host.Load();
    Check(host.Count("framework:Update")==1,"offscreen tracking alone subscribes to framework");
    host.Raise("framework:Update",host.Framework);
    Check(host.PlayerEnumerations==1&&host.Errors==0,"standalone tracking performs a real snapshot without native status/gauge calls");
    host.Config.Support.Features.Remove(83);
    typeof(Plugin).GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(host.Count("framework:Update")==0,"disabling last tracking feature unsubscribes");
}
{
    var host=new Host {Pvp=false};
    using var plugin=host.Load();
    host.Config.Support.MuteSeconds=123;
    var before=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    host.Commands["/pvpmute"].Handler("/pvpmute","");
    var until=(double)typeof(Plugin).GetField("muteUntil",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
    Check(until-before>=123&&until-before<124,"mute command consumes configured duration");
}
{
    var host=new Host {Pvp=false,ConfigDirectory=Path.GetFullPath("artifacts/review-audit-"+Guid.NewGuid().ToString("N"))};
    var dir=Path.Combine(host.ConfigDirectory,"support-review");Directory.CreateDirectory(dir);
    File.WriteAllText(Path.Combine(dir,"matches.json"),"""
    [{"Started":"2026-10-19T00:00:00Z","Ended":"2026-10-19T00:05:00Z","Note":"old-format","Settings":null,"FullSeconds":5,"Opportunities":2,"Peak":3,"Commands":{"/pvplowhp":2},"Failures":{"out of range":1},"Deaths":[{"At":"2026-10-19T00:01:00Z","Enemies":3,"Allies":1,"Survival":4.5,"States":[{"Hp":2000,"Mp":1000,"Controls":[1343]}]}]}]
    """);
    host.Config.Support.Features=[95];
    using var plugin=host.Load();
    var type=typeof(Plugin);
    var history=(System.Collections.IList)type.GetField("reviewHistory",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
    Check(history.Count==1,"export-only mode loads existing summaries without recording enabled");
    var json=System.Text.Json.JsonSerializer.Serialize(history[0]);
    Check(json.Contains("1343")&&json.Contains("4.5")&&json.Contains("out of range"),"old anonymous death objects deserialize into typed review details");
    type.GetMethod("FinishReview",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(history.Count==1,"recording never enabled does not create a summary");
    var counters=(Dictionary<string,long>)type.GetField("commandCounts",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
    counters["/pvplowhp"]=3;
    type.GetMethod("ReconcileLedger",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(counters["/pvplowhp"]==3,"unrelated configuration changes do not erase live counters when recording is off");
}
{
    var host=new Host {Pvp=true};host.Config.ShowMaxCount=false;host.Config.Support.KuzushiDisplay=false;
    using var plugin=host.Load();
    Check(host.Count("framework:Update")==0&&host.ObjectReads==0,"Kuzushi parent off stops legacy-only monitoring without clearing saved line/marker choices");
    host.Config.Support.KuzushiDisplay=true;
    typeof(Plugin).GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(host.Count("framework:Update")==1,"Kuzushi parent on restores saved legacy displays");
    host.Config.Support.ZantetsukenSupport=false;
    host.Commands["/kuzushizantetsuken"].Handler("/kuzushizantetsuken","");
    Check(host.ObjectReads==0,"LB support parent blocks command before native/game object access");
}
{
    var host=new Host {Pvp=false};var plugin=host.Load();
    host.Config.ShowLine=!host.Config.ShowLine;
    typeof(Plugin).GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    var path=Path.Combine(host.ConfigDirectory,"settings.json");
    Check(!File.Exists(path),"UI change does not immediately save settings");
    host.Raise("client:Logout",0,0);
    Check(File.Exists(path)&&File.ReadAllText(path).Contains("ShowLine"),"logout flush persists pending config");
    var first=File.GetLastWriteTimeUtc(path);
    host.Raise("client:Logout",0,0);plugin.Dispose();
    Check(File.GetLastWriteTimeUtc(path)==first&&host.TotalSubscriptions==0,"duplicate logout and dispose cause no extra write and remove all callbacks");
    var viewType=typeof(Plugin).Assembly.GetType("KuzushiBeacon.HistoryFilesView")!;
    var view=Activator.CreateInstance(viewType,host.ConfigDirectory)!;
    var root=Path.Combine(host.ConfigDirectory,"support-review");Directory.CreateDirectory(root);
    File.WriteAllBytes(Path.Combine(root,"synthetic.json"),new byte[123]);
    viewType.GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(view,null);
    ((Task)viewType.GetField("task",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!).GetAwaiter().GetResult();
    var rows=((string Path,long Count,long Bytes)[])viewType.GetField("rows",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!;
    Check(rows.Sum(r=>r.Count)==1&&rows.Sum(r=>r.Bytes)==123,"metadata totals exclude settings and include exact logical history bytes");
    ((IDisposable)view).Dispose();
}
{
    var host=new Host {Pvp=false};host.Config.Support.Features=[86];
    var plugin=host.Load();var started=DateTimeOffset.UtcNow.AddDays(-1);
    typeof(Plugin).GetField("reviewStarted",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,(DateTimeOffset?)started);
    plugin.Dispose();
    var files=Directory.GetFiles(Path.Combine(host.ConfigDirectory,"support-review"),"*.review.json",SearchOption.AllDirectories);
    Check(files.Length==1&&files[0].Contains(started.UtcDateTime.ToString("yyyyMMdd")),"dispose completes pending review save in start-day partition");
    Check(((Task)typeof(Plugin).GetField("reviewWrite",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!).IsCompleted&&host.TotalSubscriptions==0,"review writer joined and subscriptions removed before dispose returns");
}
foreach(var reason in new[]{"pvp","disabled","job","dead","missing"})
{
    var host=new Host {Pvp=reason!="pvp",Job=reason=="job"?0u:Rules.SamuraiJob,Dead=reason=="dead",PlayerAvailable=reason!="missing"};
    host.Config.Language="ja";
    if(reason=="disabled")host.Config.Enabled=false;
    using var plugin=host.Load();
    for(int i=0;i<10;i++)host.Commands["/kuzushizantetsuken"].Handler("/kuzushizantetsuken","");
    var expected=reason switch {"pvp"=>"PvPコンテンツ", "disabled"=>"オフ", "job"=>"侍では", "dead"=>"戦闘不能", _=>"情報を取得"};
    Check(host.ChatTexts.Count==1 && host.ChatTexts[0].Contains(expected),"real command localized reason and burst throttle: "+reason);
    Check(host.PlayerEnumerations==0 && host.Errors==0,"invalid prerequisites avoid scans and native calls: "+reason);
}

foreach(var master in new[]{false,true})
{
    var host=new Host {Pvp=true,DutyStarted=true};
    host.Config.ShowLine=host.Config.ShowMarker=host.Config.ShowMaxCount=false;
    host.Config.Support.Features=[86,90];
    using var plugin=host.Load();var type=typeof(Plugin);
    Check(type.GetField("reviewStarted",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!=null,"review starts only while enabled in a duty");
    var counts=(Dictionary<string,long>)type.GetField("commandCounts",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
    counts["zantetsuken"]=2;
    if(master)host.Config.Enabled=false;else host.Config.Support.SetFeature(86,false);
    type.GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    ((Task)type.GetField("reviewWrite",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!).GetAwaiter().GetResult();
    var root=Path.Combine(host.ConfigDirectory,"support-review");
    var files=Directory.Exists(root)?Directory.GetFiles(root,"*.review.json",SearchOption.AllDirectories):[];
    Check(files.Length==1 && File.ReadAllText(files[0]).Contains("zantetsuken"),"turning recording off preserves the already collected segment");
    type.GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(Directory.GetFiles(root,"*.review.json",SearchOption.AllDirectories).Length==1,"disabled reconfigure does not write duplicate review");
}
foreach(var stop in new[]{"job","leave","disable"})
{
    var host=new Host {Pvp=false};using var plugin=host.Load();var type=typeof(Plugin);
    var adapter=type.GetField("zantetsukenAction",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
    var gate=(RequestGate)adapter.GetType().GetProperty("Gate")!.GetValue(adapter)!;
    var update=typeof(IFramework).GetEvent("Update")!;
    var callback=Delegate.CreateDelegate(update.EventHandlerType!,plugin,type.GetMethod("ObserveRequest",BindingFlags.NonPublic|BindingFlags.Instance)!);
    update.AddEventHandler(host.Framework,callback);
    type.GetField("observingRequest",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,true);
    gate.Begin(0);
    if(stop=="job")host.Raise("client:ClassJobChanged",0u);
    else if(stop=="leave")host.Raise("client:LeavePvP");
    else {host.Config.Enabled=false;type.GetMethod("MarkDirty",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);}
    Check(!gate.Pending && host.Count("framework:Update")==0,"stop removes pending LB diagnostic callback: "+stop);
    var reads=host.ObjectReads;host.Raise("framework:Update",host.Framework);
    Check(host.ObjectReads==reads && host.Errors==0,"no native/player observation after stop: "+stop);
}

Console.WriteLine($"PASS {checks} host lifecycle checks (real Plugin + thread-enforcing service doubles).");

sealed class Host
{
    private readonly int mainThread = Environment.CurrentManagedThreadId;
    private readonly Dictionary<string, List<Delegate>> events = [];
    private object? ui;
    private object? player;
    public Queue<(Action Action, TaskCompletionSource Completion)> Queue { get; } = [];
    public readonly Dictionary<string, CommandInfo> Commands = [];
    public Configuration Config=new(){Language="ja"};
    public string ConfigDirectory=Path.GetFullPath("artifacts/host-config-"+Guid.NewGuid().ToString("N"));
    public bool Pvp;
    public bool ThrowOnRead;
    public bool PlayerAvailable = true;
    public int ValidityReads;
    public int PlayerEnumerations;
    public uint Job = Rules.SamuraiJob;
    public int ObjectReads;
    public int IllegalReads;
    public int Errors;
    public int ChatMessages;
    public List<string> ChatTexts = [];
    public bool Dead;
    public bool FeedbackTargetPresent;
    public System.Numerics.Vector3 FeedbackTargetPosition=new(9,1,2);
    public bool DutyStarted;
    public IFramework Framework { get; }
    public Host() => Framework = Stub<IFramework>("framework");
    public Plugin Load()
    {
        var plugin=new Plugin(Stub<IDalamudPluginInterface>("pi"), Stub<IClientState>("client"),
        Stub<IObjectTable>("objects"), Framework, Stub<IGameGui>("gui"),
        Stub<ICommandManager>("commands"), Stub<IPluginLog>("log"), Stub<ITargetManager>("targets"), Stub<IChatGui>("chat"), Stub<IPlayerState>("playerState"), Stub<IDutyState>("duty"), Stub<IDataManager>("data"), Stub<IGameInteropProvider>("interop"));
        typeof(Plugin).GetField("readFullLb",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,(Func<bool>)(()=>{GaugeReads++;return FullLb;}));
        return plugin;
    }
    public bool FullLb;
    public int GaugeReads;
    // Monitoring counts exclude the cheap 60-second settings scheduler; disposal still counts all callbacks.
    public int Count(string key) => events.TryGetValue(key, out var list) ? list.Count(d=>d.Method.Name!="PersistenceUpdate") : 0;
    public int TotalSubscriptions => events.Values.Sum(x => x.Count);
    public void Raise(string key, params object?[] args)
    {
        if (events.TryGetValue(key, out var list))
            foreach (var callback in list.ToArray()) callback.DynamicInvoke(args);
    }
    public void Drain()
    {
        while (Queue.TryDequeue(out var work))
        {
            try { work.Action(); work.Completion.SetResult(); }
            catch (Exception ex) { work.Completion.SetException(ex); }
        }
    }
    private T Stub<T>(string role) where T : class => (T)Stub(typeof(T), role);
    private object Stub(Type type, string role)
    {
        var value = DispatchProxy.Create(type, typeof(ServiceProxy));
        ((ServiceProxy)value).Call = (method, args) => Invoke(role, method, args);
        return value;
    }
    private object? Invoke(string role, MethodInfo method, object?[] args)
    {
        var name = method.Name;
        if (name.StartsWith("add_") || name.StartsWith("remove_"))
        {
            var key = role + ":" + name[(name.IndexOf('_') + 1)..];
            if (!events.TryGetValue(key, out var list)) events[key] = list = [];
            if (name.StartsWith("add_")) list.Add((Delegate)args[0]!);
            else list.Remove((Delegate)args[0]!);
            return null;
        }
        switch (name)
        {
            case "Print": ChatMessages++; ChatTexts.Add(args[0]?.ToString() ?? ""); return null;
            case "AddHandler": Commands.Add((string)args[0]!, (CommandInfo)args[1]!); return true;
            case "RemoveHandler": return Commands.Remove((string)args[0]!);
            case "GetPluginConfig": return Config;
            case "GetPluginConfigDirectory": return ConfigDirectory;
            case "get_UiLanguage": return "en";
            case "get_ClientLanguage": return ClientLanguage.Japanese;
            case "get_IsLoggedIn": return true;
            case "get_IsPvPExcludingDen": return Pvp;
            case "get_FontAtlas": return Stub(method.ReturnType,"fontAtlas");
            case "NewDelegateFontHandle": return Stub(method.ReturnType,"fontHandle");
            case "get_UiBuilder": return ui ??= Stub(method.ReturnType, "ui");
            case "get_IsInFrameworkUpdateThread": return Environment.CurrentManagedThreadId == mainThread;
            case "RunOnFrameworkThread":
                var action = (Action)args[0]!;
                if (Environment.CurrentManagedThreadId == mainThread) { action(); return Task.CompletedTask; }
                var completion = new TaskCompletionSource();
                Queue.Enqueue((action, completion));
                return completion.Task;
            case "get_LocalPlayer":
                if (Environment.CurrentManagedThreadId != mainThread) { IllegalReads++; throw new InvalidOperationException("Not on main thread!"); }
                ObjectReads++;
                if (ThrowOnRead) throw new InvalidOperationException("Simulated host failure");
                return PlayerAvailable ? player ??= Stub(method.ReturnType, "player") : null;
            case "IsValid": ValidityReads++; return true;
            case "get_Item": return FeedbackTargetPresent&&(int)args[0]! ==37?Stub(method.ReturnType,"feedbackTarget"):null;
            case "get_Position": return role=="feedbackTarget"?FeedbackTargetPosition:System.Numerics.Vector3.Zero;
            case "get_GameObjectId": return role=="feedbackTarget"?500UL:123UL;
            case "get_EntityId": return role=="feedbackTarget"?500u:123u;
            case "get_IsDead": return Dead;
            case "get_IsDutyStarted": return DutyStarted;
            case "get_PlayerObjects": PlayerEnumerations++; return Array.Empty<Dalamud.Game.ClientState.Objects.Types.IBattleChara>();
            case "get_ClassJob": return new RowRef<ClassJob>(null!, Job, null);
            case "Error": Interlocked.Increment(ref Errors); return null;
        }
        return method.ReturnType == typeof(void) ? null
            : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class ServiceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Call = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args ?? []);
}
