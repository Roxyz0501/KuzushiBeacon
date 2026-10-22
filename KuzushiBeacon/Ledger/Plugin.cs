using System.Collections.Concurrent;
using System.Threading.Channels;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace KuzushiBeacon.Ledger;

internal sealed partial class LedgerModule : IDisposable
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IDutyState duty;
    private readonly IDataManager data;
    private readonly IFramework framework;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly System.Action save;
    private LocalizedFonts fonts;
    private readonly HistoryStore store;
    private readonly Webhook webhook = new();
    private readonly NativeResults native;
    private readonly CommandInfo command;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Channel<SaveJob> jobs = Channel.CreateBounded<SaveJob>(new BoundedChannelOptions(16) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly ConcurrentQueue<System.Action> completions = new();
    private readonly ConcurrentQueue<(Guid Id, FinalResult Result)> resultQueue = new();
    private readonly Task worker;
    private MatchSession? current;
    private MatchRecord? lastMatch;
    private readonly List<SaveJob> failed = [];
    private string owner = "", status = "", exportPath = "", webhookDraft = "";
    private bool open, preview, disposed, ticking;
    private volatile bool allowDiscord;
    private DateTimeOffset nextTick, endDue;
    private readonly List<Task> auxiliary=[];
    private int pendingJobs;
    private int saveFailures, readFailures;
    private DateTimeOffset nextReadAttempt;
    public int StorageRevision => store.Revision;
    private void RunTracked(System.Action action)
    {
        if(disposed)return;
        auxiliary.RemoveAll(t=>t.IsCompleted);
        auxiliary.Add(Task.Run(action));
    }
    private uint finishedTerritory;
    private int lastGauge = -1, lastKills = -1;
    private int historyRevision = -1, page, count, historyGeneration;
    private MatchSummary[] history = [];
    private MatchRecord? detail;
    private bool loading, detailLoading, exporting;
    private int eventPage;
    private sealed record SaveJob(string Owner, MatchRecord Record, bool Send, string Secret, string Language, bool Name);

    public LedgerModule(IDalamudPluginInterface pi, ICommandManager commands, IClientState client, IPlayerState player,
        IDutyState duty, IDataManager data, IFramework framework, IChatGui chat, IGameInteropProvider interop, IPluginLog log, Configuration settings, System.Action save)
    {
        this.pi = pi; this.commands = commands; this.client = client; this.player = player; this.duty = duty;
        this.data = data; this.framework = framework; this.chat = chat; this.log = log;
        config = settings; this.save = save; config.Sanitize();
        allowDiscord = config.DiscordEnabled;
        var language = Loc.Resolve(config.Language, client.ClientLanguage, pi.UiLanguage);
        if (config.Language != language) { config.Language = language; save(); }
        fonts = new(pi.UiBuilder.FontAtlas, []);
        store = new(Path.Combine(pi.GetPluginConfigDirectory(), "ledger-matches-v1"));
        native = new(interop, () => current, (id, result) => { if (resultQueue.Count < 16) resultQueue.Enqueue((id, result)); }, log);
        command = new((_, _) => Open()) { HelpMessage = T("CommandHelp") };
        
        worker = Task.Run(Worker);
        pi.UiBuilder.Draw += Draw;
        client.EnterPvP += StartTicks; client.LeavePvP += OnLeft; chat.LogMessage += OnLog;
        duty.DutyStarted += OnStart; duty.DutyRecommenced += OnRecommence; duty.DutyCompleted += OnEnd;
        client.TerritoryChanged += OnTerritory; client.Login += OnLogin; client.Logout += OnLogout;
        OnLogin(); if(client.IsPvPExcludingDen)StartTicks();
    }
    private string T(string key) => Loc.Get(config.Language, key);
    private void Open() { open = true; StartTicks(); if (historyRevision != store.Revision) LoadHistory(); }
    public void SetVisible(bool visible) { open=visible; if(visible)StartTicks(); }
    private void StartTicks() { if(disposed||ticking)return;framework.Update+=Update;ticking=true; }
    private void OnLeft() { DrainResults(); Finish(current?.Ended==null); lastMatch=null; }
    private void QueueCompletion(System.Action action)
    {
        if(!disposed)completions.Enqueue(action);
    }
    private void OnLogin()
    {
        if (!player.IsLoaded || player.ContentId == 0) return;
        var nextOwner = HistoryStore.OwnerKey(player.ContentId);
        if (owner != nextOwner)
        {
            owner = nextOwner; historyGeneration++; loading = detailLoading = false;
            history = []; detail = null; lastMatch = null; count = page = 0; historyRevision = -1;
        }
        if (duty.IsDutyStarted && current == null && finishedTerritory != client.TerritoryType) Start(true);
        if (open) LoadHistory();
    }
    private void OnLogout(int _, int __)
    {
        DrainResults();
        Finish(true); FlushJobs(); owner = ""; history = []; detail = null; lastMatch = null; historyGeneration++;
        loading = detailLoading = false; historyRevision = -1; count = page = 0; finishedTerritory = 0;
    }
    private void OnTerritory(uint territory)
    {
        DrainResults();
        if (current != null && current.Territory != territory) Finish(current.Ended == null);
        if (finishedTerritory != territory) finishedTerritory = 0;
    }
    private void OnStart(IDutyStateEventArgs _) => Start(false);
    private void OnRecommence(IDutyStateEventArgs _) { DrainResults(); Finish(true); finishedTerritory = 0; Start(false); }
    private void OnEnd(IDutyStateEventArgs _)
    {
        if (current == null || current.Ended != null) return;
        current.End(DateTimeOffset.UtcNow); endDue = DateTimeOffset.UtcNow.AddSeconds(8);
    }
    private void Start(bool partial)
    {
        if (disposed || saveFailures>=3 || current != null || !client.IsPvPExcludingDen || !player.IsLoaded || player.ContentId == 0) return;
        try
        {
            var cfc = duty.ContentFinderCondition.Value;
            var type = data.GetExcelSheet<InstanceContent>().GetRow(cfc.Content.RowId).InstanceContentType.RowId;
            Mode? mode = type switch { 5 => Mode.CrystallineConflict, 6 => Mode.Frontline, 12 => Mode.RivalWings, _ => null };
            if (mode == null) return;
            owner = HistoryStore.OwnerKey(player.ContentId);
            StartTicks();
            current = new(DateTimeOffset.UtcNow, mode.Value, cfc.Name.ExtractText(), player.CharacterName, owner, player.ContentId, client.TerritoryType, partial);
            lastMatch = null; lastGauge = lastKills = -1; finishedTerritory = 0;
            RefreshFonts([current.Arena]);
        }
        catch { status = "StartFailed"; }
    }
    private void OnResult(Guid id, FinalResult result)
    {
        if (current == null || current.Id != id) return;
        if (current.Apply(result, DateTimeOffset.UtcNow)) Finish(false);
    }
    private void DrainResults()
    {
        while (resultQueue.TryDequeue(out var item)) OnResult(item.Id, item.Result);
    }
    private void Finish(bool interrupted)
    {
        if (current == null) return;
        var record = current.Snapshot(DateTimeOffset.UtcNow, interrupted);
        var job = new SaveJob(current.Owner, record, config.DiscordEnabled && record.Summary.Official,
            config.ProtectedWebhook, config.Language!, config.DiscordIncludeName);
        finishedTerritory = current.Territory; current = null; lastMatch = record;
        StartTicks();
        Interlocked.Increment(ref pendingJobs);
        if (!jobs.Writer.TryWrite(job)) jobs.Writer.WriteAsync(job).AsTask().GetAwaiter().GetResult();
    }
    private async Task Worker()
    {
        await foreach (var job in jobs.Reader.ReadAllAsync())
        {
            try
            {
                if(saveFailures>=3) {lock(failed)failed.Add(job);continue;}
                store.Save(job.Owner, job.Record);
                QueueCompletion(() => { status = "Saved"; if (open && owner == job.Owner) LoadHistory(); });
                if (!job.Send || !allowDiscord || cancellation.IsCancellationRequested) {saveFailures=0;continue;}
                string url;
                try { url = SecretStore.Unprotect(job.Secret); }
                catch { QueueCompletion(() => status = "SecretFailed"); continue; }
                if (!store.ClaimDelivery(job.Owner, job.Record.Summary)) continue;
                var result = await webhook.Send(url, job.Record.Summary, job.Language, job.Name, cancellation.Token).ConfigureAwait(false);
                store.DeliveryResult(job.Owner, job.Record.Summary, result);saveFailures=0;
                QueueCompletion(() => status = result);
            }
            catch
            {
                lock(failed)failed.Add(job with { Send = false });
                QueueCompletion(() => status = "SaveFailed");
                if(++saveFailures==1||saveFailures==3)log.Warning("PvP Ledger save failed; retained for manual retry. Automatic saves stop after three failures.");
            }
            finally { Interlocked.Decrement(ref pendingJobs); }
        }
    }
    private void FlushJobs()
    {
        // Worker never waits on framework callbacks; shutdown can safely join it.
        SpinWait.SpinUntil(()=>Volatile.Read(ref pendingJobs)==0);
    }
    private void Update(IFramework _)
    {
        DrainResults();
        while (completions.TryDequeue(out var complete)) if (!disposed) complete();
        if(!client.IsPvPExcludingDen&&!open&&current==null&&lastMatch==null&&Volatile.Read(ref pendingJobs)==0&&auxiliary.All(t=>t.IsCompleted)&&completions.IsEmpty)
        {framework.Update-=Update;ticking=false;return;}
        var now = DateTimeOffset.UtcNow;
        if (now < nextTick) return; nextTick = now.AddMilliseconds(250);
        if (current is { Ended: not null } && now >= endDue) { Finish(false); return; }
        if (current == null)
        {
            if (lastMatch != null && now - lastMatch.Summary.Ended > TimeSpan.FromSeconds(15)) lastMatch = null;
            return;
        }
        if (current.Mode == Mode.Frontline && config.EstimateAssists && current.Ended == null)
        {
            var gauge = native.ReadBattleHigh();
            if (gauge != null && (gauge != lastGauge || current.Kills != lastKills))
            { current.Estimate.Observe(gauge.Value, current.Kills, current.Partial); lastGauge = gauge.Value; lastKills = current.Kills; }
        }
    }
    private bool IsLocal(ILogMessageEntity? entity) => entity is { IsPlayer: true } && entity.Name.ExtractText() == player.CharacterName && entity.HomeWorldId == player.HomeWorld.RowId;
    private void OnLog(ILogMessage message)
    {
        if (message.LogMessageId is not (557 or 558 or 559) || !client.IsPvPExcludingDen) return;
        if (current == null && duty.IsDutyStarted && finishedTerritory != client.TerritoryType) Start(true);
        if (current == null || current.Ended != null) return;
        var source = message.SourceEntity; var target = message.TargetEntity;
        // 557: source defeats target. 558/559: source is defeated (by target).
        var killer = message.LogMessageId == 557 ? source : target;
        var victim = message.LogMessageId == 557 ? target : source;
        if (victim is { IsPlayer: true } && IsLocal(killer)) current.Add(DateTimeOffset.UtcNow, EventKind.Kill, victim.Name.ExtractText());
        else if (IsLocal(victim)) current.Add(DateTimeOffset.UtcNow, EventKind.Death, killer?.Name.ExtractText() ?? "");
    }
    private void ChangeConfig() { config.Sanitize(); SaveConfig(); }
    private void SaveConfig()
    {
        try { save(); }
        catch { status = "ConfigFailed"; }
    }
    private void LoadHistory()
    {
        if (disposed || owner.Length == 0 || loading || readFailures>=3 || DateTimeOffset.UtcNow<nextReadAttempt) return;
        loading = true; var generation = historyGeneration; var requestedOwner = owner; var requestedPage = page;
        RunTracked(() =>
        {
            try
            {
                var result = store.Page(requestedOwner, requestedPage);
                QueueCompletion(() =>
                {
                    if (generation != historyGeneration || requestedOwner != owner) return;
                    loading = false; readFailures=0;nextReadAttempt=default; history = result.Rows; count = result.Count; historyRevision = result.Revision;
                    RefreshFonts(history.Select(s => s.Arena));
                    if (historyRevision != store.Revision) LoadHistory();
                });
            }
            catch { QueueCompletion(() => { if (generation == historyGeneration) { loading = false;readFailures++;nextReadAttempt=DateTimeOffset.UtcNow.AddSeconds(30); status = "ReadFailed"; } }); }
        });
    }
    private void LoadDetail(MatchSummary summary)
    {
        if (detailLoading || owner.Length == 0) return;
        detailLoading = true; var generation = historyGeneration; var requestedOwner = owner;
        RunTracked(() =>
        {
            try { var result = store.Detail(requestedOwner, summary); QueueCompletion(() => { if (generation == historyGeneration) { detail = result; eventPage = 0; detailLoading = false; } }); }
            catch { QueueCompletion(() => { if (generation == historyGeneration) { status = "ReadFailed"; detailLoading = false; } }); }
        });
    }
    private void Export(bool events)
    {
        if (exporting || owner.Length == 0 || (events && detail == null)) return;
        exporting = true; var requestedOwner = owner; var rows = history; var selected = events ? detail : null;
        RunTracked(() =>
        {
            try { var path = store.Export(requestedOwner, rows, selected); QueueCompletion(() => { exporting = false; exportPath = path; status = "Exported"; }); }
            catch { QueueCompletion(() => { exporting = false; status = "ExportFailed"; }); }
        });
    }
    private void RefreshFonts(IEnumerable<string> extra)
    {
        var replacement = new LocalizedFonts(pi.UiBuilder.FontAtlas, extra);
        fonts.Dispose(); fonts = replacement;
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        pi.UiBuilder.Draw -= Draw;
        framework.Update -= Update; ticking=false; client.EnterPvP-=StartTicks; client.LeavePvP-=OnLeft; chat.LogMessage -= OnLog; duty.DutyStarted -= OnStart;
        duty.DutyRecommenced -= OnRecommence; duty.DutyCompleted -= OnEnd;
        client.TerritoryChanged -= OnTerritory; client.Login -= OnLogin; client.Logout -= OnLogout;
        native.Dispose();
        DrainResults();
        while (completions.TryDequeue(out var complete)) complete();
        Finish(true);

        disposed = true;  cancellation.Cancel(); jobs.Writer.TryComplete();
        // Disk-only queue drains on unload. Cancelling HTTP prevents the ten-second network timeout.
        worker.GetAwaiter().GetResult();
        Task.WhenAll(auxiliary).GetAwaiter().GetResult();auxiliary.Clear();
        while(completions.TryDequeue(out _)){}
        lock(failed)
            foreach(var job in failed)
                try{store.Save(job.Owner,job.Record);}catch{log.Warning("Unsaved match remains unavailable on disk; check storage permissions and capacity.");break;}
        webhook.Dispose(); cancellation.Dispose(); fonts.Dispose();
    }
}
