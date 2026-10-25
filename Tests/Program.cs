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

var oldPurify = JsonConvert.DeserializeObject<SupportOptions>("{\"PurifyBeforeZantetsuken\":true}")!;
oldPurify.Sanitize();Check(!oldPurify.PurifyBeforeZantetsuken,"saved enabled auto Purify is disabled on upgrade");
Check(CommandFeedback.Format("ja","bind|silence").Contains("バインド") && CommandFeedback.Format("ja","bind|silence").Contains("沈黙"),"observed simultaneous disabling effects remain distinguishable");
Check(CommandFeedback.StatusReason(1345)=="bind" && CommandFeedback.StatusReason(1347)=="silence" && CommandFeedback.StatusReason(99999)=="","known status mapping and unknown status handling");
foreach(var language in Loc.Languages)
{
    Check(CommandFeedback.Format(language.Code,"native:123").Contains("123"),"native diagnostic code preserved in each language");
    Check(!CommandFeedback.Format(language.Code,"range").Contains("{0}") && CommandFeedback.Format(language.Code,"unconfirmed").Length>20,"localized reason and uncertain outcome text");
}
Check(CommandFeedback.Format(null,"unexpected") == CommandFeedback.Format("en","action rejected"),"unknown condition/language fallback is honest");
{
    var w=new FakeWorld(); var a=new FakeAction(); string why="";
    bool Run(int min=1,Func<Actor,bool>? include=null) => Zantetsuken.Execute(true,1,Vector3.Zero,0,w,a,min,include,r=>why=r);
    Check(!Run() && why=="no eligible Kuzushi","no observed own Kuzushi reason");
    w.Actors.Add(new(2,2,2,new(2,0,0),true,true));w.Statuses[2]=(3202,1,2);
    Check(!Run(include:_=>false) && why=="excluded","exclusion distinct from absent Kuzushi");
    a.Blocked.Add(2);Check(!Run() && why=="range, LoS or action unavailable","selection failure retained");a.Blocked.Clear();
    Check(!Run(2) && why=="minimum coverage","minimum count reason");
    a.Accept=false;Check(!Run() && why=="action rejected","native rejection propagated");
    a.Accept=true;Check(Run() && a.Attempts==2,"failure diagnostics do not block next explicit execution");
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
Check(Zantetsuken.FullGauge(true, 10000, 10000, 3), "PvP requires one full personal bar, not all displayed bars");
Check(Zantetsuken.FullGauge(true, 10000, 10000, 1), "PvP full gauge");
Check(!Zantetsuken.FullGauge(true, 9999, 10000, 1), "partial gauge hidden");
Check(!Zantetsuken.FullGauge(false, 10000, 10000, 1), "PvE gauge hidden");
Check(!Zantetsuken.FullGauge(true, 0, 0, 0), "uninitialized gauge hidden");
Check(Zantetsuken.ShowCount(true, 3, 10, 9), "full LB and live own targets visible");
Check(!Zantetsuken.ShowCount(false, 3, 10, 9), "consumed LB hides existing count immediately");
Check(!Zantetsuken.ShowCount(true, 0, 10, 9), "no own targets hides number");
Check(!Zantetsuken.ShowCount(true, 3, 10, 10), "expired snapshot hides count");
{
    var actors = new[] {
        new Actor(0, 100, 100, new Vector3(10, 0, 0), true, true),
        new Actor(1, 101, 101, new Vector3(14, 0, 0), true, true),
        new Actor(2, 102, 102, new Vector3(18, 0, 0), true, true) };
    var action = new FakeAction();
    var selection = Zantetsuken.FindBest(actors, Vector3.Zero, 0, action.CanUse);
    Check(selection.Count == 3 && selection.Target?.Index == 1, "head count includes center and maximum additional targets");
    Check(action.Attempts == 0, "count-only computation never executes action");
    Check(Zantetsuken.FindBest(actors, Vector3.Zero, 0, _ => false).Count == 0, "unreachable centers produce no count");
}
{
    var gate = new RequestGate();
    Check(gate.Begin(0), "first request accepted by gate");
    Check(!gate.Begin(0.3) && !gate.Begin(1000), "issued request never resends on a timer");
    gate.Observe(true);
    Check(!gate.Begin(1001), "full LB keeps pending request latched");
    gate.Observe(false);
    Check(gate.Begin(1002), "observed consumption releases request latch");
    gate.Rejected(1002);
    Check(!gate.Begin(1002.5) && gate.Begin(1003), "confirmed rejection has retry backoff");
    Check(gate.Message(0) && !gate.Message(0.5) && !gate.Message(2.99) && gate.Message(3), "errors limited to one per three seconds");
    gate.Reset();
    Check(gate.Begin(1004), "territory/logout reset clears latch");
    // Rejected LB while some other action is queued must not become permanently pending.
    gate.Reset();gate.Begin(0);gate.Result(0,false,false,10,10);
    Check(!gate.Pending&&!gate.Begin(.9)&&gate.Begin(1),"unrelated queue is not LB acceptance; bounded rejection backoff");
    gate.Reset();gate.Begin(0);gate.Result(0,true,true,10,10);
    gate.Observe(.2,true,true,10,true,false);
    Check(gate.Pending&&!gate.Begin(.3),"LB queued locally blocks duplicate calls");
    gate.Observe(.4,true,false,10,true,false);
    Check(!gate.Pending&&!gate.Begin(.5)&&gate.Begin(1.4),"cancelled unsent queue releases after rejection backoff");
    gate.Reset();gate.Begin(0);gate.Result(0,true,true,65535,65535);
    gate.Observe(.2,true,false,0,true,false);
    Check(gate.Pending,"queued execution with wrapped sequence remains protected");
    gate.Observe(10,true,false,0,true,false);
    Check(gate.Pending,"elapsed time alone never permits an automatic retry");
    gate.Observe(10,true,false,0,false,true);
    Check(gate.Pending,"manual press during native lock cannot recover unknown result");
    gate.Observe(10,true,true,0,true,true);
    Check(gate.Pending,"manual press never replaces queued LB");
    gate.Observe(12.99,true,false,0,true,true);
    Check(gate.Pending,"manual recovery waits three seconds after queue observation");
    gate.Observe(13,true,false,0,true,true);
    Check(!gate.Pending&&gate.Begin(13),"full gauge and idle client allow fresh explicit attempt after timeout");
    gate.Result(13,true,false,0,1);
    gate.Observe(13.1,true,false,1,true,true);
    Check(gate.Pending,"fresh attempt immediately restores duplicate protection");
    gate.Observe(13.2,false,false,1,true,false);
    Check(!gate.Pending,"observed LB consumption clears pending");
    gate.Reset();gate.Begin(0);gate.Result(0,false,false,20,21);
    Check(gate.Pending,"sequence change protects issued request even when native return is false");
    gate.Observe(3,true,false,22,true,true);
    Check(!gate.Pending,"unrelated later actions do not indefinitely extend manual recovery wait");
    var countConfig = new Configuration { CountSize = float.NaN, CountHeight = 99, CountOffsetY = -999 };
    countConfig.Sanitize();
    Check(countConfig.CountSize == 32 && countConfig.CountHeight == 6 && countConfig.CountOffsetY == -300, "number settings clamped independently");
    var roundtrip = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(countConfig))!;
    Check(roundtrip.CountSize == countConfig.CountSize && roundtrip.CountOffsetY == countConfig.CountOffsetY, "number settings persist");
}
{
    HpCandidate C(int id, uint hp, float x, bool enemy = true) => new(new Actor(id, (uint)id, (ulong)id,
        new Vector3(x, 0, 0), true, true), hp, 0.5f, enemy);
    var candidates = new[] { C(2, 1000, 3), C(3, 500, 4), C(4, 1, 5), C(5, 1, 1, false) };
    Check(LowHpTarget.Find(candidates, 1, Vector3.Zero, 0.5f, 3)?.Actor.EntityId == 3, "lowest absolute HP within hitbox range, enemies only");
    Check(LowHpTarget.Find(new[] { C(2, 0, 1), C(1, 1, 1) }, 1, Vector3.Zero, 0.5f, 3) is null, "dead HP and self excluded");
    Check(LowHpTarget.Find(new[] { C(3, 500, 3), C(2, 500, 2) }, 1, Vector3.Zero, 0, 3)?.Actor.EntityId == 2, "HP tie chooses nearest");
    Check(!LowHpTarget.InRange(Vector3.Zero, 0.5f, new Vector3(4.01f, 0, 0), 0.5f, 3), "outside hitbox range excluded");
    Check(!LowHpTarget.InRange(Vector3.Zero, 0.5f, new Vector3(float.NaN, 0, 0), 0.5f, 3), "invalid positions excluded");
    Check(new Configuration().LowHpRange == 3, "user selected default 3m");
    var bounded = Enumerable.Range(2, 201).Select(i => C(i, i == 202 ? 1u : 100u, 1));
    Check(LowHpTarget.Find(bounded, 1, Vector3.Zero, 0, 3)?.Actor.EntityId == 2, "low HP enumeration capped at 200");
}

{
    var options=new SupportOptions();
    Check(!options.NeedsMonitor, "default support switches require no background scan");
    Check(SupportOptions.CommandRange(34,20)==3 && SupportOptions.CommandRange(23,50)==20 && SupportOptions.CommandRange(34,2)==2,"command ranges respect job caps");
    Check(SupportOptions.CommandRange(34,50,true)==20,"ally command range capped at 20m");
    var oversized=new SupportOptions {MeleeRange=50,RangedRange=50,AllyRange=50};oversized.Sanitize();
    Check(oversized.MeleeRange==3&&oversized.RangedRange==20&&oversized.AllyRange==20,"saved range caps");
    Check(SupportOptions.JobRange(34,3,20)==3 && SupportOptions.JobRange(41,3,20)==3 && SupportOptions.JobRange(19,3,20)==3,"melee and tanks use 3m");
    Check(SupportOptions.JobRange(23,3,20)==20 && SupportOptions.JobRange(24,3,20)==20 && SupportOptions.JobRange(42,3,20)==20,"ranged and healers use 20m");
    Check(FeatureCatalog.All.Select(f=>f.Id).Distinct().Count()==FeatureCatalog.All.Length,"unique feature switches");
    foreach(var id in Enumerable.Range(16,60).Concat(Enumerable.Range(77,24)).Where(id=>id is not (34 or 42 or 70 or 72 or 81)))
        Check(FeatureCatalog.All.Any(f=>f.Id==id),"requested feature has a switch: "+id);
    options.Features.Add(46);Check(options.NeedsMonitor,"survival feature requests monitoring");
    options.DisabledGroups.Add(3);Check(!options.On(46)&&!options.NeedsMonitor,"category disable stops its work");
    options.DisabledGroups.Clear();options.Features.Add(77);options.SmallFeatures=[];options.LargeFeatures=[46];
    Check(!options.On(46)&&options.On(46,true)&&!options.NeedsMonitor,"inactive profile does not scan");
    options.CurrentLarge=true;Check(options.NeedsMonitor&&options.On(46),"active profile enables monitoring and command conditions");
    var config=new Configuration {LowHpRange=5};config.Sanitize();Check(config.Support.MeleeRange==3&&config.Support.RangedRange==20,"legacy range migration caps melee range");
    config.Support.Features.Add(99);var saved=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(config))!;
    Check(saved.Support.On(99)&&saved.Support.MeleeRange==3,"feature options persist");
    options.Volume=float.NaN;options.MaxActors=int.MaxValue;options.UpdateMs=-1;options.Sanitize();
    Check(options.Volume==.25f&&options.MaxActors==200&&options.UpdateMs==50,"invalid settings bounded");
    var latch=new AlertLatch();
    Check(!latch.Observe(1,true,0,.5)&&!latch.Observe(1,true,.4,.5)&&latch.Observe(1,true,.5,.5),"stable condition fires once after dwell");
    Check(!latch.Observe(1,true,5,.5),"continuous condition never repeats");
    latch.Observe(1,false,6,.5);Check(!latch.Observe(1,true,7,.5)&&latch.Observe(1,true,7.5,.5),"condition rearms after loss");
    Observation O(int id,float x,uint hp,bool enemy=true)=>new(new(id,(uint)id,(ulong)id,new(x,0,0),true,true),hp,100,.5f,enemy,!enemy,0,2,0,false,false,0,false);
    var crowd=new[]{O(2,0,80),O(3,4,20),O(4,5.01f,10),O(5,1,5,false)};
    var coverage=SupportMath.Count(crowd[0],crowd,5,30);
    Check(coverage.Count==2&&coverage.LowCount==1,"area geometry excludes allies and counts HP threshold");
    var protectedCrowd = new[]{crowd[0] with {Guard=true},crowd[1] with {Invulnerable=true},crowd[2] with {Actor=crowd[2].Actor with {Position=new(1,0,0),Targetable=false}}};
    Check(SupportMath.Count(crowd[0],protectedCrowd,5,30,true,true).Count==0,"excluded and untargetable victims never inflate AoE count");
    Check(SupportMath.Count(crowd[0],protectedCrowd,5,30,false,true).Count==1,"Guard filter independently changes victim count");
    Check(SupportMath.Count(crowd[0],protectedCrowd,5,30,true,false).LowCount==1,"invulnerability filter changes low-HP victim count");
    Check(crowd[1].Distance(Vector3.Zero,.5f)==3,"hitbox adjusted displayed distance");
    Check(!(crowd[0] with{Guard=true}).Eligible(true,false)&&!(crowd[0] with{Invulnerable=true}).Eligible(false,true),"independent status exclusions");
    var w=new FakeWorld();foreach(var o in crowd.Where(o=>o.Enemy)){w.Actors.Add(o.Actor);w.Statuses[o.Actor.Index]=(3202,1,2);}
    var a=new FakeAction();
    Check(!Zantetsuken.Execute(true,1,Vector3.Zero,0,w,a,4)&&a.Attempts==0,"minimum coverage blocks native request");
    Check(!Zantetsuken.Execute(true,1,Vector3.Zero,0,w,a,2,x=>x.EntityId!=3)&&a.Attempts==0,"excluded victim is not counted");
    Check(Zantetsuken.Execute(true,1,Vector3.Zero,0,w,a,1,x=>x.EntityId!=3)&&a.Last?.EntityId!=3,"excluded center is not selected");
}
// Settings-to-runtime audit regressions.
{
    foreach(var feature in FeatureCatalog.All)
        Check(FeatureHelp.All.TryGetValue(feature.Id,out var help)&&help.Japanese.Length>10&&help.English.Length>10,"detailed bilingual help: "+feature.Id);
    var p=new SupportOptions {Features=[77,96],SmallFeatures=[96],LargeFeatures=[96],CurrentLarge=true};
    p.SetFeature(83,true);
    Check(p.On(83)&&p.Selected(83)&&p.NeedsMonitor&&p.NeedsOtherActors(false),"active profile edit starts standalone offscreen tracking");
    Check(!p.NeedsSelfStatuses&&!p.NeedsEnemyStatuses(false),"tracking does not require status scans");
    Check(!p.Features.Contains(83)&&!p.SmallFeatures.Contains(83),"editing active large profile preserves other profiles");
    p.SetFeature(83,false);Check(!p.NeedsMonitor,"turning tracking off stops its standalone monitor");
    p.SetFeature(60,true);Check(p.On(60)&&p.NeedsSelfStatuses,"death snapshots collect self statuses independently");
    Check(!p.NeedsEnemyStatuses(false),"death snapshots do not scan enemy statuses");
    p.SetFeature(77,false);Check(!p.On(60)&&!p.NeedsMonitor,"profile switch is global even with saved profiles");
    p.SetAllFeatures(true);Check(p.On(83)&&p.SmallFeatures.Contains(83)&&!p.LargeFeatures.Contains(99),"enable all updates every profile, excluding dry run");
    p.SetAllFeatures(false);Check(!p.NeedsMonitor&&p.Features.Count==0&&p.SmallFeatures.Count==0&&p.LargeFeatures.Count==0,"disable all clears every profile");
    p.Features=[96,31];Check(p.NeedsEnemyStatuses(false)&&!p.NeedsSelfStatuses,"Kuzushi labels read enemy statuses only");
    p.Features=[96,37];Check(p.NeedsSelfStatuses&&!p.NeedsOtherActors(false),"Chiten alone does not scan other actors");
    p.Features=[77];p.CurrentLarge=true;p.LargeFeatures=[79,80,83];p.SmallFeatures=[79];p.SetCategoryFeatures(5,false);
    Check(!p.Features.Contains(77)&&p.LargeFeatures.Count==0&&p.SmallFeatures.Contains(79),"category disable keeps edits on original profile when profile switch is turned off");
    p.UpdateMs=500;Check(p.SnapshotInterval(true,100)==100&&p.SnapshotInterval(false,100)==500,"legacy interval affects shared sampling");
    p.UpdateMs=50;Check(p.SnapshotInterval(true,100)==50,"shared snapshot respects faster support interval");
}
{
    var p=new SupportOptions {Features=[31,32,33,34,35,39,41,43],SmallFeatures=[34],LargeFeatures=[33,34]};p.Sanitize();
    Check(!FeatureCatalog.ById.ContainsKey(34)&&p.Features.Contains(33)&&!p.Features.Contains(34)&&p.SmallFeatures.Contains(33)&&!p.LargeFeatures.Contains(34),"duplicate acquisition notification migrates in all profiles");
    p.KuzushiDisplay=false;Check(!p.On(31)&&!p.On(32)&&p.On(41),"Kuzushi display parent does not disable independent LB support");
    p.KuzushiDisplay=true;p.OwnKuzushiDisplay=false;Check(!p.On(31)&&!p.On(32),"own-target parent gates timers");
    p.OwnKuzushiDisplay=true;p.Features.Remove(31);Check(!p.On(32),"expiry color requires timer");
    p.Features.Add(31);p.KuzushiFullLbOnly=true;Check(!p.KuzushiVisible(false)&&p.KuzushiVisible(true),"LB-only highlight condition");
    p.SamuraiNotifications=false;Check(!p.On(33)&&!p.On(35)&&p.On(39),"shared LB alert independent of Samurai alerts");
    p.DisabledGroups.Add(2);Check(p.On(39),"shared LB alert independent of Samurai group");
    p.DisabledGroups.Clear();p.ZantetsukenSupport=false;Check(!p.On(41)&&!p.On(43),"LB support parent gates its children");
    var old=new Configuration {Version=2};old.Support.DisabledGroups.Add(2);old.Sanitize();
    Check(old.Version==3&&!old.Support.KuzushiDisplay&&!old.Support.ZantetsukenSupport&&!old.Support.SamuraiNotifications&&!old.Support.DisabledGroups.Contains(2),"disabled legacy Samurai category migrates to visible parent controls");
}
{
    var pressWorld = new FakeWorld();
    pressWorld.Actors.Add(new Actor(2, 2, 2, Vector3.Zero, true, true));
    pressWorld.Statuses[2] = (3202, 1, 5);
    var action = new FakeAction { Accept = false };
    Check(!Zantetsuken.Execute(true, 1, Vector3.Zero, 0, pressWorld, action), "rejected explicit press calls action once");
    action.Accept = true;
    for (var press = 0; press < 20; press++)
        Check(Zantetsuken.Execute(true, 1, Vector3.Zero, 0, pressWorld, action), "each repeated explicit press reaches action");
    Check(action.Attempts == 21, "twenty-one presses produce exactly twenty-one attempts including initial rejection");
    pressWorld.Statuses.Clear();
    Check(!Zantetsuken.Execute(true, 1, Vector3.Zero, 0, pressWorld, action) && action.Attempts == 21,
        "each press refreshes candidates; expired Kuzushi does not execute");
}
checks += PersistenceChecks.Run();
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
