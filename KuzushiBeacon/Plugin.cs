using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Roxyz0501.Dalamud.CommonUi;

namespace KuzushiBeacon;

public sealed class Plugin : IDalamudPlugin
{
    private const string ExecuteCommand = "/kuzushizantetsuken";
    private readonly ITargetManager targets;
    private readonly IChatGui chat;
    private readonly IZantetsukenAction zantetsukenAction;
    private double nextCommand;
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
        IFramework framework, IGameGui gui, ICommandManager commands, IPluginLog log, ITargetManager targets, IChatGui chat)
    {
        this.targets = targets;
        this.chat = chat;
        this.pi = pi;
        this.client = client;
        this.objects = objects;
        this.framework = framework;
        this.gui = gui;
        this.commands = commands;
        this.log = log;
        world = new DalamudWorld(objects);
        zantetsukenAction = new GameZantetsukenAction(client, objects, targets, world);
        config = pi.GetPluginConfig() as Configuration ?? new Configuration();
        config.Sanitize();
        ClientLanguage? game;
        try { game = client.ClientLanguage; } catch { game = null; }
        Loc.CheckCompleteness();
        if (config.InitializeLanguage(game, pi.UiLanguage)) pi.SavePluginConfig(config);
        commandInfo = new CommandInfo((_, _) => Open()) { HelpMessage = T("CommandHelp") };
        commands.AddHandler(Command, commandInfo);
        executeCommandInfo = new CommandInfo((_, args) => { if (string.IsNullOrWhiteSpace(args)) ExecuteZantetsuken(); }) { HelpMessage = T("ExecuteHelp") };
        commands.AddHandler(ExecuteCommand, executeCommandInfo);
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

    private async Task ExecuteZantetsukenAsync()
    {
        var requestedAt = Now;
        try
        {
            await framework.RunOnFrameworkThread(() =>
            {
                if (disposed || Now - requestedAt > 0.5 || Now < nextCommand) return;
                nextCommand = Now + 0.25;
                if (!client.IsLoggedIn || !client.IsPvPExcludingDen) { chat.Print(T("ExecuteUnavailable")); return; }
                var player = objects.LocalPlayer;
                if (player is null || !player.IsValid() || player.IsDead || player.ClassJob.RowId != Rules.SamuraiJob)
                { chat.Print(T("ExecuteUnavailable")); return; }
                if (!Zantetsuken.Execute(true, player.EntityId, player.Position, targets.Target?.GameObjectId ?? 0,
                    world, zantetsukenAction)) chat.Print(T("ExecuteUnavailable"));
            });
        }
        catch (Exception ex)
        {
            log.Error(ex, "Zantetsuken command failed; no retry will be attempted.");
        }
    }

    private string T(string key) => Loc.Get(config.Language, key);
    private string Label(string key) => T(key) + "###" + key;
    private string Control(string key)
    {
        ImGui.TextWrapped(T(key));
        ImGui.SetNextItemWidth(-1);
        return "###" + key;
    }
    private bool MonitoringAllowed() => !disposed && !faulted && config.Enabled && (config.ShowLine || config.ShowMarker)
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
        tracker.Clear();
        trackedOwner = 0;
    }

    private void TerritoryChanged(uint _) { Stop(); Reconcile(); }
    private void JobChanged(uint _) { Stop(); Reconcile(); }
    private void Logout(int _, int __) => Stop();

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
            // LocalPlayer can disappear during zoning. Keep the PvP callback alive
            // so detection resumes without requiring another client-state event.
            var player = objects.LocalPlayer;
            if (player is null || !player.IsValid() || player.ClassJob.RowId != Rules.SamuraiJob || player.IsDead)
            {
                tracker.Clear();
                trackedOwner = 0;
                return;
            }
            trackedOwner = player.EntityId;
            tracker.Update(true, Now, player.EntityId, player.Position, config.MaxDistance, config.ScanIntervalMs, world);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void DrawOverlay()
    {
        try
        {
            if (!MonitoringAllowed() || tracker.Targets.Count == 0) return;
            var player = objects.LocalPlayer;
            if (player is null || !player.IsValid() || player.ClassJob.RowId != Rules.SamuraiJob || player.IsDead || player.EntityId != trackedOwner) return;
            var now = Now;
            var draw = ImGui.GetBackgroundDrawList();
            var lineColor = ImGui.ColorConvertFloat4ToU32(config.LineColor);
            var markerColor = ImGui.ColorConvertFloat4ToU32(config.MarkerColor);
            Vector2 from = default;
            bool fromVisible = config.ShowLine && Project(player.Position + Vector3.UnitY * 0.8f, out from);
            if (config.ShowLine && !fromVisible) projectionFailures++;
            foreach (var target in tracker.Targets)
            {
                if (now >= target.ExpiresAt) continue;
                var actor = objects[target.Actor.Index];
                if (actor is null || !actor.IsValid() || actor.EntityId != target.Actor.EntityId
                    || actor.GameObjectId != target.Actor.ObjectId || actor.IsDead || !actor.IsTargetable) continue;
                var position = actor.Position;
                if (Vector3.DistanceSquared(position, player.Position) > config.MaxDistance * config.MaxDistance) continue;
                if (fromVisible && Project(position + Vector3.UnitY * 0.8f, out var to))
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
        using var font = fonts!.Push(config.Language);
        ImGui.SetNextWindowSize(new Vector2(680, 650), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(470, 350), new Vector2(float.MaxValue));
        if (ImGui.Begin("Kuzushi Beacon###KuzushiBeacon", ref open))
        {
            if (ImGui.BeginTabBar("KuzushiBeaconTabs"))
            {
                StandardPluginTabs.DrawStandardTrailingTabs(T("Settings"), "Settings", DrawSettings,
                    T("LanguageSettings"), DrawLanguage, T("Support"), DrawSupport);
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
        if (dirty && (Now >= saveAfter || !open)) Save();
        if (!open) pi.UiBuilder.Draw -= DrawSettingsWindow;
    }

    private void DrawSettings()
    {
        ImGui.TextWrapped(T("Explanation"));
        ImGui.TextWrapped(T("ExecuteHelp"));
        ImGui.TextWrapped(T(faulted ? "Faulted" : subscribed ? "Active" : "Idle"));
        ImGui.TextWrapped(T("DetectionMetrics"));
        ImGui.TextWrapped(string.Format(T("DetectionMetricsValue"), tracker.ScanCount, tracker.CandidateReads,
            tracker.StatusListReads, tracker.Targets.Count, drawnLines, drawnMarkers, projectionFailures));
        ImGui.TextWrapped(string.Format(T("StatusDiagnostics"), world.KuzushiSeen,
            world.OwnSourceSeen, world.InvalidOwnDurationSeen));
        bool changed = false;
        if (ImGui.Checkbox(Label("Enabled"), ref config.Enabled))
        {
            faulted = false;
            changed = true;
        }
        ImGui.Separator();
        changed |= ImGui.Checkbox(Label("ShowLine"), ref config.ShowLine);
        changed |= ImGui.ColorEdit4(Control("LineColor"), ref config.LineColor, ImGuiColorEditFlags.AlphaBar);
        changed |= ImGui.SliderFloat(Control("LineWidth"), ref config.LineWidth, 0.5f, 5f, "%.1f px");
        ImGui.Separator();
        changed |= ImGui.Checkbox(Label("ShowMarker"), ref config.ShowMarker);
        changed |= ImGui.ColorEdit4(Control("MarkerColor"), ref config.MarkerColor, ImGuiColorEditFlags.AlphaBar);
        changed |= ImGui.SliderFloat(Control("MarkerSize"), ref config.MarkerSize, 5f, 30f, "%.0f px");
        changed |= ImGui.SliderFloat(Control("MarkerHeight"), ref config.MarkerHeight, 1f, 5f, "%.1f m");
        ImGui.TextWrapped(T("HeightHelp"));
        ImGui.Separator();
        changed |= ImGui.SliderFloat(Control("Distance"), ref config.MaxDistance, 5f, 100f, "%.0f m");
        changed |= ImGui.SliderInt(Control("Interval"), ref config.ScanIntervalMs, 50, 500, "%d ms");
        ImGui.TextWrapped(T("PerformanceHelp"));
        if (changed) { config.Sanitize(); MarkDirty(); }
        ImGui.Separator();
        ImGui.TextUnformatted(T("Preview"));
        var start = ImGui.GetCursorScreenPos();
        var width = Math.Max(180f, ImGui.GetContentRegionAvail().X);
        ImGui.InvisibleButton("###Preview", new Vector2(width, 75));
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + new Vector2(width, 75), 0xFF24201C, 6);
        var from = start + new Vector2(30, 55);
        var to = start + new Vector2(width - 40, 55);
        draw.AddCircleFilled(from, 5, 0xFFDDDDDD);
        draw.AddCircleFilled(to, 5, 0xFFDDDDDD);
        if (config.ShowLine) draw.AddLine(from, to, ImGui.ColorConvertFloat4ToU32(config.LineColor), config.LineWidth);
        if (config.ShowMarker) DrawMarker(draw, to - new Vector2(0, 8), Math.Min(22, config.MarkerSize),
            ImGui.ColorConvertFloat4ToU32(config.MarkerColor), config.MarkerColor.W);
        ImGui.TextWrapped(string.Format(T("Metrics"), tracker.ScanCount, tracker.CandidateReads,
            tracker.StatusListReads, tracker.LastScanMs));
    }

    private void DrawLanguage()
    {
        var code = StandardPluginTabs.DrawLanguageSelector(T("Language"), T("LanguageHelp"), config.Language ?? "en");
        if (code is null || code == config.Language) return;
        config.Language = code;
        commandInfo.HelpMessage = T("CommandHelp");
        executeCommandInfo.HelpMessage = T("ExecuteHelp");
        dirty = true;
        Save();
    }

    private void DrawSupport() => StandardPluginTabs.DrawSupportContent(
        T("SupportHeading"), T("SupportText"), T("SupportRecipient"), T("OpenKoFi"));

    public void Dispose()
    {
        disposed = true;
        Stop();
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
        commands.RemoveHandler(ExecuteCommand);
        Save();
        fonts?.Dispose();
    }
}

