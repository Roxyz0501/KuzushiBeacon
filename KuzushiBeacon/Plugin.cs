using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Roxyz0501.Dalamud.CommonUi;

namespace KuzushiBeacon;

public sealed partial class Plugin : IDalamudPlugin
{
    private const string SettingsAlias = "/pvpsupporter";
    private const string LowHpCommand = "/pvplowhp";
    private readonly GameLowHpTarget lowHpTarget;
    private readonly CommandInfo lowHpCommandInfo;
    private double nextLowHpCommand;
    private const string ExecuteCommand = "/kuzushizantetsuken";
    private readonly ITargetManager targets;
    private readonly IChatGui chat;
    private readonly GameZantetsukenAction zantetsukenAction;
    private readonly List<Actor> countCandidates = new(Tracker.MaxTargets);
    private int maximumCount;
    private readonly HashSet<ulong> eligibleHighlights=[];
    private double countExpiresAt;
    private bool observingRequest;
    private bool previewCount;
    private readonly RequestGate messages = new();
    private const string Command = "/kuzushibeacon";
    private readonly IDalamudPluginInterface pi;
    private readonly IClientState client;
    private readonly IObjectTable objects;
    private readonly IFramework framework;
    private readonly IGameGui gui;
    private readonly ICommandManager commands;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly CommandInfo commandInfo;
    private readonly CommandInfo executeCommandInfo;
    private readonly Tracker tracker = new();
    private readonly DalamudWorld world;
    private LocalizedFonts? fonts;
    private bool open;
    private bool subscribed;
    private bool dirty;
    private volatile bool disposed;
    private bool faulted;
    private double saveAfter;
    private uint trackedOwner;
    private long drawnLines;
    private long drawnMarkers;
    private long projectionFailures;
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    public Plugin(IDalamudPluginInterface pi, IClientState client, IObjectTable objects,
        IFramework framework, IGameGui gui, ICommandManager commands, IPluginLog log, ITargetManager targets, IChatGui chat,
        IPlayerState playerState, IDutyState duty, IDataManager data, IGameInteropProvider interop)
    {
        this.playerState = playerState; this.duty = duty; this.data = data; this.interop = interop;
        this.targets = targets;
        this.chat = chat;
        this.pi = pi;
        this.client = client;
        this.objects = objects;
        this.framework = framework;
        this.gui = gui;
        this.commands = commands;
        this.log = log;
        lowHpTarget = new GameLowHpTarget(client, objects, targets);
        world = new DalamudWorld(objects);
        zantetsukenAction = new GameZantetsukenAction(client, objects, targets, world);
        config = pi.GetPluginConfig() as Configuration ?? new Configuration();
        config.Sanitize();
        zantetsukenAction.Options = config.Support;
        ClientLanguage? game;
        try { game = client.ClientLanguage; } catch { game = null; }
        Loc.CheckCompleteness();
        if (config.InitializeLanguage(game, pi.UiLanguage)) pi.SavePluginConfig(config);
        commandInfo = new CommandInfo((_, _) => Open()) { HelpMessage = T("CommandHelp") };
        commands.AddHandler(Command, commandInfo);
        commands.AddHandler(SettingsAlias, commandInfo);
        lowHpCommandInfo = new CommandInfo((command, args) => _ = SelectSupportTargetAsync(command, args)) { HelpMessage = T("LowHpHelp") };
        commands.AddHandler(LowHpCommand, lowHpCommandInfo);
        executeCommandInfo = new CommandInfo((_, args) => { if (string.IsNullOrWhiteSpace(args)) ExecuteZantetsuken(); }) { HelpMessage = T("ExecuteHelp") };
        commands.AddHandler(ExecuteCommand, executeCommandInfo);
        RegisterSupportCommands();
        duty.DutyStarted += SupportDutyStarted;
        duty.DutyRecommenced += SupportDutyStarted;
        duty.DutyCompleted += SupportDutyEnded;
        client.TerritoryChanged += TerritoryChanged;
        client.ClassJobChanged += JobChanged;
        client.EnterPvP += Reconcile;
        client.LeavePvP += Stop;
        client.Login += Reconcile;
        client.Logout += Logout;
        pi.UiBuilder.OpenConfigUi += Open;
        pi.UiBuilder.OpenMainUi += Open;
        Reconcile();
    }

    private void ExecuteZantetsuken() => _ = ExecuteZantetsukenAsync();

    private async Task ExecuteZantetsukenAsync(bool targetOnly = false)
    {
        try
        {
            await framework.RunOnFrameworkThread(() =>
            {
                if (disposed) return;
                if (!config.Enabled || !S.ZantetsukenSupport || !client.IsLoggedIn || !client.IsPvPExcludingDen) { CommandUnavailable(); return; }
                var player = objects.LocalPlayer;
                if (player is null || !player.IsValid() || player.IsDead || player.ClassJob.RowId != Rules.SamuraiJob)
                { CommandUnavailable(); return; }
                if (!GameZantetsukenAction.IsLimitBreakFull()) { CountCommand(targetOnly?"ztarget":"zantetsuken",false,"LB not full"); CommandUnavailable(); return; }
                zantetsukenAction.TargetOnly = targetOnly;
                try
                {
                    var reason="";
                    var success = Zantetsuken.Execute(true, player.EntityId, player.Position, targets.Target?.GameObjectId ?? 0,
                        world, zantetsukenAction, On(43)?S.MinimumZantetsuken:1, zantetsukenAction.Included,r=>reason=r);
                    CountCommand(targetOnly?"ztarget":"zantetsuken",success,reason);
                    if(zantetsukenAction.Gate.Pending)requestPhase=1;
                    if (!success) CommandUnavailable();
                    else if(On(99) && messages.Message(Now))chat.Print(L("検証：斬鉄剣の候補を選択しました（操作なし）","Dry run: selected a Zantetsuken candidate; no action performed."));
                }
                finally
                {
                    zantetsukenAction.TargetOnly = false;
                    if (zantetsukenAction.Gate.Pending && !observingRequest)
                    { framework.Update += ObserveRequest; observingRequest = true; }
                }
            });
        }
        catch (Exception ex)
        {
            if (messages.Message(Now)) log.Error(ex, "Zantetsuken command failed; no retry will be attempted.");
        }
    }

    private void CommandUnavailable()
    {
        if (messages.Message(Now)) chat.Print(T("ExecuteUnavailable"));
    }

    private void ObserveRequest(IFramework _)
    {
        try
        {
            if (!client.IsLoggedIn || !client.IsPvPExcludingDen) zantetsukenAction.Gate.Reset();
            else if (GameZantetsukenAction.TryReadGauge(out var full))
            {
                zantetsukenAction.ObserveRequest(Now, full);
                if (!full) requestPhase = 2;
                else if (!zantetsukenAction.Gate.Pending) requestPhase = 0;
            }
        }
        catch (Exception ex) { if (messages.Message(Now)) log.Error(ex, "Could not observe pending LB request; retry remains blocked."); }
        if (!zantetsukenAction.Gate.Pending && observingRequest)
        { framework.Update -= ObserveRequest; observingRequest = false; }
    }

    private void ResetRequest()
    {
        zantetsukenAction.Gate.Reset();requestPhase=0;
        if (observingRequest) framework.Update -= ObserveRequest;
        observingRequest = false;
    }

    private string T(string key) => Loc.Get(config.Language, key);
    private string Label(string key) => T(key) + "###" + key;
    private string Control(string key)
    {
        ImGui.TextWrapped(T(key));
        ImGui.SetNextItemWidth(-1);
        return "###" + key;
    }
    private bool OwnHighlightsEnabled => !S.DisabledGroups.Contains(2)&&S.KuzushiDisplay&&S.OwnKuzushiDisplay&&(config.ShowLine||config.ShowMarker);
    private bool CountHighlightEnabled => !S.DisabledGroups.Contains(2)&&S.ZantetsukenSupport&&config.ShowMaxCount;
    private bool MonitoringAllowed() => !disposed && !faulted && config.Enabled && ((OwnHighlightsEnabled||CountHighlightEnabled) || S.NeedsMonitor)
        && client.IsLoggedIn && client.IsPvPExcludingDen;

    private void Reconcile() => _ = ReconcileAsync();

    private async Task ReconcileAsync()
    {
        if (disposed) return;
        try
        {
            // Constructors run on the installer worker thread. ObjectTable must only be
            // accessed on the framework thread, even when loading directly into PvP.
            await framework.RunOnFrameworkThread(() =>
            {
                if (disposed) return; // Unloaded before the queued first tick.
                ReconcileLedger();
                if (!MonitoringAllowed()) { Stop(); return; }
                if (subscribed) return;
                tracker.Clear();
                framework.Update += Update;
                pi.UiBuilder.Draw += DrawOverlay;
                subscribed = true;
            });
        }
        catch (Exception ex)
        {
            if (!disposed) Fail(ex);
        }
    }

    private void Stop()
    {
        if (subscribed)
        {
            framework.Update -= Update;
            pi.UiBuilder.Draw -= DrawOverlay;
            subscribed = false;
        }
        ClearSupport();
        tracker.Clear();
        maximumCount = 0;
        countExpiresAt = 0;
        countCandidates.Clear();eligibleHighlights.Clear();
        trackedOwner = 0;
    }

    private void TerritoryChanged(uint _) { reviewCompleted=false; FinishReview(); ResetRequest(); Stop(); Reconcile(); }
    private void JobChanged(uint _) { Stop(); Reconcile(); }
    private void Logout(int _, int __) { FinishReview(); ResetRequest(); Stop(); }

    private void Fail(Exception ex)
    {
        faulted = true;
        Stop();
        log.Error(ex, "Kuzushi overlay stopped. Toggle Enable in settings to retry.");
    }

    private void Update(IFramework _)
    {
        try
        {
            if (!MonitoringAllowed()) { Stop(); return; }
            SupportUpdate(Now);
            // LocalPlayer can disappear during zoning. Keep the PvP callback alive
            // so detection resumes without requiring another client-state event.
            var player = objects.LocalPlayer;
            if (!(OwnHighlightsEnabled||CountHighlightEnabled) || S.DisabledGroups.Contains(2) || player is null || !player.IsValid() || player.ClassJob.RowId != Rules.SamuraiJob || player.IsDead)
            {
                tracker.Clear();
                maximumCount = 0;
                trackedOwner = 0;
                return;
            }
            trackedOwner = player.EntityId;
            var previousScan = tracker.ScanCount;
            var now = Now;
            // Reuse one bounded scan for both overlays. Count must not depend on highlight distance.
            tracker.Update(true, now, player.EntityId, player.Position,
                CountHighlightEnabled ? float.MaxValue : config.MaxDistance, config.ScanIntervalMs,
                sharedSnapshot ? new SnapshotWorld(observations,player.EntityId,now-supportAt) : world);
            if (previousScan == tracker.ScanCount) return;
            eligibleHighlights.Clear();
            foreach(var target in tracker.Targets)
            {
                var eligible=sharedSnapshot
                    ? observations.Any(o=>o.Actor.ObjectId==target.Actor.ObjectId&&o.Eligible(S.ZantetsukenExcludeGuard,S.ZantetsukenExcludeInvulnerable))
                    : zantetsukenAction.Included(target.Actor);
                if(eligible)eligibleHighlights.Add(target.Actor.ObjectId);
            }
            maximumCount = 0;
            countExpiresAt = 0;
            if (CountHighlightEnabled && tracker.Targets.Count > 0 && GameZantetsukenAction.IsLimitBreakFull())
            {
                countCandidates.Clear();
                countExpiresAt = double.MaxValue;
                foreach (var target in tracker.Targets)
                {
                    if (now >= target.ExpiresAt || !eligibleHighlights.Contains(target.Actor.ObjectId)) continue;
                    if(!sharedSnapshot)countCandidates.Add(target.Actor);
                    countExpiresAt = Math.Min(countExpiresAt, target.ExpiresAt);
                }
                maximumCount = sharedSnapshot ? supportLbCount : Zantetsuken.FindBest(countCandidates, player.Position,
                    targets.Target?.GameObjectId ?? 0, zantetsukenAction.CanReach).Count;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void DrawOverlay()
    {
        try
        {
            DrawSupportOverlay();
            if (!MonitoringAllowed() || tracker.Targets.Count == 0) return;
            var player = objects.LocalPlayer;
            if (player is null || !player.IsValid() || player.ClassJob.RowId != Rules.SamuraiJob || player.IsDead || player.EntityId != trackedOwner) return;
            var now = Now;
            var draw = ImGui.GetBackgroundDrawList();
            if (CountHighlightEnabled && maximumCount > 0
                && Zantetsuken.ShowCount(GameZantetsukenAction.IsLimitBreakFull(), maximumCount, countExpiresAt, now)
                && Project(player.Position + Vector3.UnitY * config.CountHeight, out var countHead))
                DrawCount(ImGui.GetForegroundDrawList(), countHead + new Vector2(0, config.CountOffsetY), maximumCount);
            if(!OwnHighlightsEnabled||!S.KuzushiVisible(!S.KuzushiFullLbOnly||GameZantetsukenAction.IsLimitBreakFull()))return;
            var lineColor = ImGui.ColorConvertFloat4ToU32(config.LineColor);
            var markerColor = ImGui.ColorConvertFloat4ToU32(config.MarkerColor);
            Vector2 from = default;
            bool fromVisible = config.ShowLine && Project(player.Position + Vector3.UnitY * config.LineHeight, out from);
            if (config.ShowLine && !fromVisible) projectionFailures++;
            foreach (var target in tracker.Targets)
            {
                if (now >= target.ExpiresAt) continue;
                var actor = objects[target.Actor.Index];
                if (actor is null || !actor.IsValid() || actor.EntityId != target.Actor.EntityId
                    || actor.GameObjectId != target.Actor.ObjectId || actor.IsDead || !actor.IsTargetable) continue;
                if(!eligibleHighlights.Contains(target.Actor.ObjectId))continue;
                var position = actor.Position;
                if (Vector3.DistanceSquared(position, player.Position) > config.MaxDistance * config.MaxDistance) continue;
                if (fromVisible && Project(position + Vector3.UnitY * config.LineHeight, out var to))
                {
                    draw.AddLine(from, to, lineColor, config.LineWidth);
                    drawnLines++;
                }
                else if (fromVisible) projectionFailures++;
                if (config.ShowMarker && Project(position + Vector3.UnitY * config.MarkerHeight, out var head))
                {
                    DrawMarker(draw, head, config.MarkerSize, markerColor, config.MarkerColor.W);
                    drawnMarkers++;
                }
                else if (config.ShowMarker) projectionFailures++;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void DrawCount(ImDrawListPtr draw, Vector2 anchor, int count)
    {
        var text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var scale = config.CountSize / ImGui.GetFontSize();
        var size = ImGui.CalcTextSize(text) * scale;
        var position = anchor - new Vector2(size.X * 0.5f, size.Y);
        draw.AddRectFilled(position - new Vector2(5, 3), position + size + new Vector2(5, 3), 0xB0000000, 4);
        draw.AddText(ImGui.GetFont(), config.CountSize, position, ImGui.ColorConvertFloat4ToU32(config.CountColor), text);
    }

    private bool Project(Vector3 position, out Vector2 screen)
        => gui.WorldToScreen(position, out screen) && float.IsFinite(screen.X) && float.IsFinite(screen.Y);

    private static void DrawMarker(ImDrawListPtr draw, Vector2 tip, float size, uint color, float alpha)
    {
        var left = tip + new Vector2(-size, -size * 1.5f);
        var right = tip + new Vector2(size, -size * 1.5f);
        draw.AddTriangleFilled(left, right, tip, color);
        draw.AddTriangle(left, right, tip, ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.1f, alpha)), 1.2f);
    }

    private void Open()
    {
        if (disposed || open) return;
        fonts ??= new LocalizedFonts(pi.UiBuilder.FontAtlas);
        open = true;
        pi.UiBuilder.Draw += DrawSettingsWindow;
    }

    private void MarkDirty()
    {
        dirty = true;
        saveAfter = Now + 0.6;
        tracker.Clear();
        nextSupport=0;maximumCount=0;countExpiresAt=0;
        if(!On(60))preDeath.Clear();
        if(!On(93))lowHpSince=0;
        foreach(var id in alerts.Keys.Where(id=>!On(id)||!NotificationsAllowed).ToArray())alerts.Remove(id);
        if(!S.Sound||!NotificationsAllowed)localSound.Stop();
        Reconcile();
    }

    private void Save()
    {
        if (!dirty) return;
        // Do not blindly retry a write whose outcome is unknown.
        dirty = false;
        try { pi.SavePluginConfig(config); }
        catch (Exception ex) { log.Error(ex, "Could not save Kuzushi Beacon settings."); }
    }

    private void DrawSettingsWindow()
    {
        ledger?.SetVisible(false);
        using var font = fonts!.Push(config.Language);
        ImGui.SetNextWindowSize(new Vector2(1120, 780), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(470, 350), new Vector2(float.MaxValue));
        if (ImGui.Begin("PvP Supporter###KuzushiBeacon", ref open))
        {
            if (ImGui.BeginTabBar("KuzushiBeaconTabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                if(ImGui.BeginTabItem("PvP Supporter###Supporter"))
                {DrawSettingsNavigation();ImGui.EndTabItem();}
                StandardPluginTabs.DrawStandardTrailingTabs(T("Settings"), "Settings", DrawSettings,
                    T("LanguageSettings"), DrawLanguage, T("Support"), DrawSupport);
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
        if (dirty && (Now >= saveAfter || !open)) Save();
        if (!open) { ledger?.SetVisible(false); pi.UiBuilder.Draw -= DrawSettingsWindow; }
    }

    private void DrawSettings()
    {
        ImGui.TextWrapped(T("Explanation"));
        ImGui.TextWrapped(T(faulted ? "Faulted" : subscribed ? "Active" : "Idle"));
        if(ImGui.Checkbox(Label("Enabled"), ref config.Enabled)){faulted=false;MarkDirty();}
        if(ImGui.Button(L("支援機能を全てオン（検証モードを除く）","Enable all support features (except dry run)")))
        {config.Enabled=true;S.SetAllFeatures(true);S.KuzushiDisplay=S.OwnKuzushiDisplay=S.ZantetsukenSupport=S.SamuraiNotifications=true;config.ShowLine=config.ShowMarker=config.ShowMaxCount=true;MarkDirty();}
        if(ImGui.Button(L("支援機能を全てオフ","Disable all support features")))
        {config.Enabled=false;S.SetAllFeatures(false);config.ShowLine=config.ShowMarker=config.ShowMaxCount=false;MarkDirty();}
        if(ImGui.Checkbox(L("通知をまとめてオン","Enable all notifications"),ref S.Notifications))MarkDirty();
        if(ImGui.Checkbox(L("SEをまとめてオン","Enable notification audio"),ref S.Sound))MarkDirty();
        ImGui.TextWrapped(L("設定は左側のメニューに分かれています。無効な機能の通知は発生しません。","Settings are organized in the left menu. Disabled features do not notify."));
    }
    private void DrawLegacy(int section)
    {
        bool changed=false;
        if(section==7&&(OwnHighlightsEnabled||CountHighlightEnabled))
        {
        if(OwnHighlightsEnabled)changed |= ImGui.SliderFloat(Control("Distance"), ref config.MaxDistance, 5f, 100f, "%.0f m");
        changed |= ImGui.SliderInt(Control("Interval"), ref config.ScanIntervalMs, 50, 500, "%d ms");
        ImGui.TextWrapped(T("PerformanceHelp"));
        ImGui.TextWrapped(L("他の支援機能と収集を共有するときは短い方の間隔で収集します。","When sharing with other support features, collection uses the shorter interval."));
        ImGui.TextWrapped(string.Format(T("Metrics"), tracker.ScanCount, tracker.CandidateReads,
            tracker.StatusListReads, tracker.LastScanMs));
        }
        if(changed){config.Sanitize();MarkDirty();}
    }

    private void DrawLanguage()
    {
        var code = StandardPluginTabs.DrawLanguageSelector(T("Language"), T("LanguageHelp"), config.Language ?? "en");
        if (code is null || code == config.Language) return;
        config.Language = code;
        config.Ledger.Language=code;
        commandInfo.HelpMessage = T("CommandHelp");
        executeCommandInfo.HelpMessage = T("ExecuteHelp");
        lowHpCommandInfo.HelpMessage = T("LowHpHelp");
        dirty = true;
        Save();
    }

    private void DrawSupport() => StandardPluginTabs.DrawSupportContent(
        T("SupportHeading"), T("SupportText"), T("SupportRecipient"), T("OpenKoFi"));

    public void Dispose()
    {
        FinishReview();
        disposed = true;
        ledger?.Dispose(); ledger=null;
        foreach(var name in supportCommands)commands.RemoveHandler(name);
        localSound.Dispose();
        reviewWrite.GetAwaiter().GetResult();
        ResetRequest();
        Stop();
        duty.DutyStarted -= SupportDutyStarted;
        duty.DutyRecommenced -= SupportDutyStarted;
        duty.DutyCompleted -= SupportDutyEnded;
        client.TerritoryChanged -= TerritoryChanged;
        client.ClassJobChanged -= JobChanged;
        client.EnterPvP -= Reconcile;
        client.LeavePvP -= Stop;
        client.Login -= Reconcile;
        client.Logout -= Logout;
        pi.UiBuilder.OpenConfigUi -= Open;
        pi.UiBuilder.OpenMainUi -= Open;
        pi.UiBuilder.Draw -= DrawSettingsWindow;
        commands.RemoveHandler(Command);
        commands.RemoveHandler(SettingsAlias);
        commands.RemoveHandler(LowHpCommand);
        commands.RemoveHandler(ExecuteCommand);
        Save();
        fonts?.Dispose();
    }
}

