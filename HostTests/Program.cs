using System.Reflection;
using Dalamud.Game;
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
    Check(host.ObjectReads == (pvp ? 1 : 0), "PvP gate short circuits before ObjectTable");
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
    SpinWait.SpinUntil(() => host.Errors == 1, 1000);
    Check(host.Errors == 1, "deferred failure is observed and logged");
    Check(host.Count("framework:Update") == 0 && host.Count("ui:Draw") == 0, "deferred failure stops monitor");
    plugin.Dispose();
    Check(host.TotalSubscriptions == 0, "failed deferred initialization stays disposable");
}
Console.WriteLine($"PASS {checks} host lifecycle checks (real Plugin + thread-enforcing service doubles).");

sealed class Host
{
    private readonly int mainThread = Environment.CurrentManagedThreadId;
    private readonly Dictionary<string, List<Delegate>> events = [];
    private object? ui;
    private object? player;
    public Queue<(Action Action, TaskCompletionSource Completion)> Queue { get; } = [];
    public bool Pvp;
    public bool ThrowOnRead;
    public int ObjectReads;
    public int IllegalReads;
    public int Errors;
    public IFramework Framework { get; }
    public Host() => Framework = Stub<IFramework>("framework");
    public Plugin Load() => new(Stub<IDalamudPluginInterface>("pi"), Stub<IClientState>("client"),
        Stub<IObjectTable>("objects"), Framework, Stub<IGameGui>("gui"),
        Stub<ICommandManager>("commands"), Stub<IPluginLog>("log"));
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
            case "GetPluginConfig": return new Configuration { Language = "ja" };
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
                return player ??= Stub(method.ReturnType, "player");
            case "get_ClassJob": return new RowRef<ClassJob>(null!, Rules.SamuraiJob, null);
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
