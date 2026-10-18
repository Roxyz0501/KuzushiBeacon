using System.Numerics;
using System.Diagnostics;
using KuzushiBeacon;
using Dalamud.Game;
using Newtonsoft.Json;

int checks = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception(name);
    checks++;
}

Check(Rules.CanRun(true, true, true, true, true, 34), "samurai in PvP");
foreach (var state in new[] {
    (false,true,true,true,true,34u), (true,false,false,true,true,34u),
    (true,true,true,false,true,34u), (true,true,true,true,false,34u), (true,true,true,true,true,1u) })
    Check(!Rules.CanRun(state.Item1,state.Item2,state.Item3,state.Item4,state.Item5,state.Item6), "inactive gate");
Check(Rules.OwnStatus(3202, 10, 10, 3), "own Kuzushi");
Check(!Rules.OwnStatus(3202, 11, 10, 3), "other samurai");
Check(!Rules.OwnStatus(3201, 10, 10, 3), "other status");
foreach (var duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
    Check(!Rules.OwnStatus(3202, 10, 10, duration), "invalid duration");
foreach (var id in new[] { 0u, 0xE0000000u })
    Check(!Rules.OwnStatus(3202, id, id, 3), "invalid source");

var world = new FakeWorld();
var tracker = new Tracker();
for (var i = 0; i < 36000; i++) tracker.Update(false, i / 60d, 10, Vector3.Zero, 50, 100, world);
Check(world.Enumerations == 0 && world.StatusReads == 0 && tracker.ScanCount == 0,
    "ten inactive minutes: zero candidate or status reads");
world.Actors.AddRange([
    new(1, 10, 10, Vector3.Zero, true, true), // self
    new(2, 11, 11, new(5,0,0), true, true), // own
    new(3, 12, 12, new(5,0,0), true, true), // another SAM
    new(4, 13, 13, new(51,0,0), true, true), // too far
    new(5, 14, 14, new(5,0,0), false, true), // dead
    new(6, 15, 15, new(5,0,0), true, false), // untargetable
    new(7, 16, 16, new(float.NaN,0,0), true, true)]);
world.Statuses[2] = (3202, 10, 4);
world.Statuses[3] = (3202, 99, 4);
tracker.Update(true, 0, 10, Vector3.Zero, 50, 100, world);
Check(tracker.Targets.Count == 1 && tracker.Targets[0].Actor.Index == 2, "only own valid enemy selected");
Check(world.StatusReads == 2, "status access only after cheap filters");
for (int i = 1; i < 100; i++) tracker.Update(true, i / 1000d, 10, Vector3.Zero, 50, 100, world);
Check(world.Enumerations == 1, "no repeated scan within interval");
Check(tracker.Targets[0].ExpiresAt <= .151, "stale cache deadline");
world.Statuses.Remove(2);
tracker.Update(true, .1, 10, Vector3.Zero, 50, 100, world);
Check(tracker.Targets.Count == 0, "dispel clears at next scan");
world.Statuses[2] = (3202, 10, .02f);
tracker.Update(true, .21, 10, Vector3.Zero, 50, 100, world);
Check(tracker.Targets[0].ExpiresAt < .231, "natural expiration does not wait for next scan");
tracker.Update(true, .22, 99, Vector3.Zero, 50, 100, world);
Check(tracker.Targets.Count == 1 && tracker.Targets[0].Actor.Index == 3, "owner change invalidates cache immediately");
tracker.Update(false, .23, 99, Vector3.Zero, 50, 100, world);
Check(tracker.Targets.Count == 0, "PvP leave or disable clears");

world = new FakeWorld();
for (int i = 1; i <= 500; i++)
{
    world.Actors.Add(new(i, (uint)(100 + i), (ulong)(100 + i), Vector3.One, true, true));
    world.Statuses[i] = (3202, 10, 4);
}
tracker = new Tracker();
tracker.Update(true, 0, 10, Vector3.Zero, 50, 100, world);
Check(tracker.Targets.Count == 200 && world.StatusReads == 200, "bounded cache and candidates");

world = new FakeWorld();
for (int i = 1; i <= 72; i++)
{
    world.Actors.Add(new(i, (uint)(100 + i), (ulong)(100 + i), Vector3.One, true, true));
    world.Statuses[i] = (3202, i % 10 == 0 ? 10u : 99u, 4);
}
tracker = new Tracker();
var watch = Stopwatch.StartNew();
for (int i = 0; i < 6000; i++) tracker.Update(true, i / 600d, 10, Vector3.Zero, 50, 100, world);
watch.Stop();
Check(tracker.ScanCount <= 100 && tracker.ScanCount >= 95, "10s at 600 FPS: at most 100 scans");
Check(world.StatusReads <= 7200, "72 players: at most 7200 status-list reads in 10s");
Console.WriteLine($"SIMULATION 10s / 6000 frames / 72 players: scans={tracker.ScanCount}, candidates={tracker.CandidateReads}, statusLists={world.StatusReads}, hostElapsedMs={watch.Elapsed.TotalMilliseconds:F3}");

Loc.CheckCompleteness();
foreach (var (code, _) in Loc.Languages)
{
    var c = new Configuration { Language = code, ShowLine = false, ShowMarker = true, LineWidth = 2.5f,
        LineColor = new Vector4(.1f, .2f, .3f, .4f) };
    var restored = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(c))!;
    Check(!restored.InitializeLanguage(ClientLanguage.English, "ja") && restored.Language == code, "saved language " + code);
    Check(!restored.ShowLine && restored.ShowMarker && restored.LineWidth == 2.5f && restored.LineColor == c.LineColor,
        "independent toggles, alpha and size persist " + code);
    Check(Loc.Get(code, "Metrics").Contains("{3:F3}"), "metrics format " + code);
}
foreach (var pair in new[] { ("ja-JP","ja"), ("en_US","en"), ("de-DE","de"), ("fr-FR","fr"),
    ("ko-KR","ko"), ("zh-CN","zh-Hans"), ("zh-SG","zh-Hans"), ("zh-TW","zh-Hant"),
    ("zh-HK","zh-Hant"), ("zh-MO","zh-Hant"), ("ZH-hans","zh-Hans") })
    Check(Loc.Normalize(pair.Item1) == pair.Item2, "normalize " + pair.Item1);
Check(Loc.Normalize("zh") is null && Loc.Normalize("invalid") is null, "ambiguous and unsupported language");
var cfg = new Configuration();
Check(cfg.InitializeLanguage(ClientLanguage.Japanese, "de") && cfg.Language == "ja", "game before Dalamud");
cfg.Language = "Auto";
Check(cfg.InitializeLanguage(null, "zh_CN") && cfg.Language == "zh-Hans", "Dalamud fallback");
cfg.Language = null;
Check(cfg.InitializeLanguage(null, "zh") && cfg.Language == "en", "English fallback");
cfg.LineWidth = float.NaN; cfg.MarkerSize = 1000; cfg.LineColor = new(float.NaN, -10, 10, float.PositiveInfinity);
cfg.ScanIntervalMs = 0; cfg.Sanitize();
Check(cfg.LineWidth == 1.5f && cfg.MarkerSize == 30 && cfg.LineColor == new Vector4(1,0,1,1)
    && cfg.ScanIntervalMs == 50, "invalid config clamped");
// Zantetsuken: maximize a target-centered 5m circle, never use a stale overlay cache.
{
    var w = new FakeWorld();
    Actor A(int i, float x, float y = 0) => new(i, (uint)(i + 100), (ulong)(i + 100), new Vector3(x, y, 0), true, true);
    void Add(Actor a, uint source = 10) { w.Actors.Add(a); w.Statuses[a.Index] = (3202, source, 2); }
    Add(A(0, 10)); Add(A(1, 14)); Add(A(2, 18)); Add(A(3, 0));
    var action = new FakeAction();
    Check(!Zantetsuken.Execute(false, 10, Vector3.Zero, 0, w, action) && w.Enumerations == 0 && action.Checks == 0, "command inactive has zero reads/actions");
    Check(Zantetsuken.Execute(true, 10, Vector3.Zero, 100, w, action) && action.Last?.Index == 1 && action.Attempts == 1, "maximum coverage beats current/nearest target");
    action.Blocked.Add(1);
    Check(Zantetsuken.Execute(true, 10, Vector3.Zero, 102, w, action) && action.Last?.Index == 2, "unusable centers excluded, current target wins tied coverage");
    Check(Zantetsuken.Execute(true, 10, Vector3.Zero, 0, w, action) && action.Last?.Index == 0, "nearest wins tied coverage");
    action.Blocked.Clear();
    w.Statuses[1] = (3202, 999, 2);
    Check(Zantetsuken.Execute(true, 10, Vector3.Zero, 101, w, action) && action.Last?.Index == 3, "other source neither selected nor counted");
    w.Actors.Clear(); w.Statuses.Clear();
    Add(A(0, 0)); Add(A(1, 5)); Add(A(2, 5.01f));
    Check(Zantetsuken.Execute(true, 10, Vector3.Zero, 100, w, action) && action.Last?.Index == 1, "inclusive 5m boundary, outside excluded");
    w.Actors[2] = A(2, 0, 10);
    Check(Zantetsuken.Execute(true, 10, Vector3.Zero, 100, w, action) && action.Last?.Index == 0, "vertical separation is not counted as overlap");
    action.Accept = false;
    var attempts = action.Attempts;
    Check(!Zantetsuken.Execute(true, 10, Vector3.Zero, 100, w, action) && action.Attempts == attempts + 1, "revalidation/execution failure never retries another target");
    w.Statuses.Clear();
    attempts = action.Attempts;
    Check(!Zantetsuken.Execute(true, 10, Vector3.Zero, 100, w, action) && action.Attempts == attempts, "no current own status never acts");
    w.Actors.Clear();
    for (int i = 0; i < 500; i++) Add(A(i, i));
    var reads = w.StatusReads; var evaluations = action.Checks;
    Zantetsuken.Execute(true, 10, Vector3.Zero, 0, w, action);
    Check(w.StatusReads - reads == 200 && action.Checks - evaluations == 200, "command scan and native availability checks bounded to 200");
}
Console.WriteLine($"PASS {checks} checks; localization complete for {Loc.Languages.Length} languages.");

sealed class FakeWorld : IHighlightWorld
{
    public List<Actor> Actors { get; } = [];
    public Dictionary<int, (uint Status, uint Source, float Remaining)> Statuses { get; } = [];
    public int Enumerations;
    public int StatusReads;
    public IEnumerable<Actor> Players { get { Enumerations++; return Actors; } }
    public float OwnKuzushiRemaining(int index, uint sourceId)
    {
        StatusReads++;
        return Statuses.TryGetValue(index, out var s) && Rules.OwnStatus(s.Status, s.Source, sourceId, s.Remaining)
            ? s.Remaining : 0;
    }
}

sealed class FakeAction : IZantetsukenAction
{
    public readonly HashSet<int> Blocked = [];
    public int Checks, Attempts;
    public bool Accept = true;
    public Actor? Last;
    public bool CanUse(Actor target) { Checks++; return !Blocked.Contains(target.Index); }
    public bool TryExecute(Actor target, uint owner) { Attempts++; Last = target; return Accept; }
}
