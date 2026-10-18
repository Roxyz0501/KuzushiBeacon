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
        typeof(Plugin).GetField("nextCommand", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(plugin, 0d);
        host.Commands["/kuzushizantetsuken"].Handler("/kuzushizantetsuken", "");
    }
    Check(host.ChatMessages == 1, "real command suppresses repeated unavailable messages");
    Check(host.ObjectReads == 0, "unavailable command spam never scans objects");
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
    type.GetField("reviewStarted",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,(DateTimeOffset?)DateTimeOffset.UtcNow);
    type.GetMethod("FinishReview",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);
    Check(history.Count==1,"recording disabled never appends a pending summary");
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
    public string ConfigDirectory=Path.GetFullPath("artifacts/host-config");
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
    public IFramework Framework { get; }
    public Host() => Framework = Stub<IFramework>("framework");
    public Plugin Load() => new(Stub<IDalamudPluginInterface>("pi"), Stub<IClientState>("client"),
        Stub<IObjectTable>("objects"), Framework, Stub<IGameGui>("gui"),
        Stub<ICommandManager>("commands"), Stub<IPluginLog>("log"), Stub<ITargetManager>("targets"), Stub<IChatGui>("chat"), Stub<IPlayerState>("playerState"), Stub<IDutyState>("duty"), Stub<IDataManager>("data"), Stub<IGameInteropProvider>("interop"));
    public int Count(string key) => events.TryGetValue(key, out var list) ? list.Count : 0;
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
            case "Print": ChatMessages++; return null;
            case "AddHandler": Commands.Add((string)args[0]!, (CommandInfo)args[1]!); return true;
            case "RemoveHandler": return Commands.Remove((string)args[0]!);
            case "GetPluginConfig": return Config;
            case "GetPluginConfigDirectory": return ConfigDirectory;
            case "get_UiLanguage": return "en";
            case "get_ClientLanguage": return ClientLanguage.Japanese;
            case "get_IsLoggedIn": return true;
            case "get_IsPvPExcludingDen": return Pvp;
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
            case "get_EntityId": return 123u;
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
