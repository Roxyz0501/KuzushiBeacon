# 1.1.0.1 validation — 2026-10-19

- 205 managed checks pass, including job/fixed command caps, oversized saved-range correction, and existing targeting/request gates.
- 48 real-host lifecycle checks pass; no additional idle scans introduced.
- 84 headless production UI frames pass: eight feature groups and ten sidebar pages in Japanese/English. Job settings and command-copy pages rendered; zero unchanged config writes. Common top-tab implementation unchanged.
- Release build succeeds; no live-game or OS clipboard interaction validation. Copy uses ImGui SetClipboardText and does not dispatch a command.
- Command-only feature flags 42/70/72/81 removed from the catalog and execution gates. Global enable, PvP/living-player gates, dry-run and request throttling retained; local mute remains usable outside PvP.
- Enemy selection caps: melee/tank 3m, ranged/healer 20m; allies 20m. General targeting is not a guarantee that a particular action can hit. Zantetsuken retains native availability checks.

# 1.1.0.0 expansion validation — 2026-10-19

## Executed

- Managed feature/regression suite: **206 checks PASS**. Job ranges, migration, active content profiles, individual/group switches, stable alert edges/rearming, hitbox distance, area/low-HP counting, minimum LB coverage and exclusions, plus existing source-ID/gauge/request-gate tests.
- Host lifecycle suite: **48 checks PASS** using the real Plugin constructor and thread-enforcing doubles. All new targeting commands registered; all features enabled outside PvP still produce zero object reads/enumerations and no hidden initialization errors. Leave/unload removes callbacks; missing local player recovery preserved.
- Integrated Ledger suite: **2,781 checks PASS** against this assembly. Result parsing, observed/official KDA separation, inference replay, retention/page caches, character isolation, CSV escaping, DPAPI, durable duplicate delivery prevention. HTTP is a fake handler; no actual Discord messages sent.
- Ledger I/O measured: first page 25 summary reads, cached opens +0 reads/+0 writes/+0 bytes, concurrent new page +25 reads, detail +1 read. Test summary bytes 8,700; 60 saved matches generated 120 file writes. 2,000 inference cases: 16.3 ms in this run (test host, not gameplay).
- Headless production UI: **44 frames PASS**. Eight category panels in Japanese/English, real main settings and a synthetic support HUD. Zero config writes during unchanged rendering. All generated support glyphs present in all four regional host font faces. Local artifacts under artifacts/ui-final-1100 (ignored by Git).
- Release build: zero warnings/errors. Common StandardPluginTabs.cs unchanged. Package has the explicit eight-file allowlist, including both integrated license notices; no host DLLs, player records, webhook data or test artifacts.

## Inspection / operational boundaries

- Every gameplay command checks PvP and alive local player before scanning; 200-candidate cap; final identity/target eligibility/range checks. Target/UseAction calls are only in explicit command handlers. Existing request latch protects ambiguous native requests from resending.
- Zantetsuken geometry is behind full-LB checks. General AoE is a separate opt-in analysis. Draw reads cached coverage; it never invokes area search or UseAction. When support scanning is active, legacy Kuzushi tracking uses the shared value snapshot and LB coverage result.
- Local audio uses bounded PCM and per-sample volume, no game mixer modification. Notification priorities are applied after evaluating the batch; stable conditions and cooldowns prevent repeated notification.
- New feature controls are Japanese/English; other common language selections use English for new controls. Existing and integrated Ledger strings retain seven-language coverage.
- Native action/readiness, status IDs/ownership, incoming result signatures and real scene rendering were **not exercised in the live game**. No assertion of actual kills, accurate hit outcomes, safe routes or live CPU timings is made. Test timing is not gameplay performance evidence.
- Range/invulnerability tables are explicit and limited. Current local sheets checked Guard 3054/3673, Kuzushi 3202, Chiten 1240, Resilience 3248, Undead Redemption 3039; Guard 29054, Purify 29056, Elixir 29055, Recuperate 29711 / 2000 MP. Unknown effects are not inferred.
- Incoming result code is copied from the user's PvP Ledger with its build guard and upstream notices. It observes only; no outbound packet API, packet construction, bypass or resource writes were added.
- Published ZIP/index verification is recorded in PUBLICATION_STATE.md after publication. Installed plugin files are not replaced with unpublished builds.

---

# Validation — 2026-10-19

## 1.0.0.7 PvP Supporter rename, low-HP target, request latch and visible count

- Display name is PvP Supporter; InternalName, existing repository/ZIP, config path and old commands are unchanged. New /pvpsupporter settings alias and /pvplowhp target-only command.
- Low-HP command: user-selected default range 3m, configurable 1–50m; absolute current HP, hitbox-adjusted 3D distance, enemy player classification through native ClassifyTarget. Fresh maximum 200-player scan only on explicit invocation, no status reads or automatic attacks; revalidates identity/life/range before setter. Does not check walls or invulnerability.
- 102 managed + 38 host checks pass. Added minimum HP, range boundary, friend/self/dead exclusion, ties, malformed coordinates, 200 cap, default range, command aliases and zero outside-PvP reads. Native classification/target selection and renamed installer flow remain unverified in game.


- RequestGate is set before targeting/UseAction. Accepted calls, existing queue, changed action sequence or ambiguous exceptions remain latched until valid gauge depletion is observed. Confirmed rejection allows manual retry after 1s. Pending observation reads only gauge fields; no enemy scans or network hooks, stops on consumption/territory/logout/disposal, and also works with overlays disabled.
- Plugin error chat/log messages share a 3s limiter. Existing command ingress debounce (250ms) remains. No global game-error suppression.
- Count moved from background to foreground with dark backing and independent color/size/height/screen-Y settings plus settings-only live preview. Marker alpha and height can no longer hide the count inadvertently. PvP fullness compares CurrentUnits to one BarUnits instead of multiplying by BarCount; corroborated with upstream field declarations and a public PvP readiness implementation, not live memory observation.
- Request-gate regression checks cover: pending latch beyond any timeout, continued full gauge, observed depletion, rejected-request delay, message window, config clamping/roundtrip and real Plugin command spam with zero outside-PvP object reads. Native acceptance, gate values and actual display remain unverified in game; no logged overlay exception explained the reported hidden number.
- Canceled accepted queues with no LB consumption remain latched until territory/logout/reload; conservative handling avoids retransmitting an ambiguous request.

## 1.0.0.6 hotbar execution mode

- Upstream ActionManager.UseActionMode.None (0) explicitly describes usual execution such as a hotbar press; Macro (2) suppresses queueing. Adapter now uses None for the single UseAction request.
- Existing target selection, preflight checks and duplicate suppression remain. No synthetic UI click, custom packet or custom retry. Client's standard queue/check behavior is permitted; preflight checks may reject before reaching UseAction.
- 85 managed and 34 host checks pass; Release zero warnings/errors. Native hotbar parity/queue timing remain unverified in game. Earlier sections describe their historical versions.

## 1.0.0.5 overhead maximum count

- Gauge read uses host LimitBreakController IsPvP, CurrentUnits, BarUnits and BarCount; requires positive units/bars and current >= units * bars. No action execution is used to query readiness. Full gauge is distinct from temporary action lock.
- Shared FindBest selection returns maximum total own-Kuzushi count including center. Display uses native reach/target eligibility without GetActionStatus, so cast locks do not imply an empty gauge. No target setter/UseAction on the display path.
- Existing Tracker scan is reused, with up to 200 player/status candidates independent of highlight distance while count is enabled. Maximum 40,000 distance comparisons per scan; zero additional enemy/status scans during drawing. Numeric drawing rechecks only gauge fields and cached expiry. Full calculation occurs at ScanIntervalMs (default 100ms); if no own targets or LB is not full, no coverage calculation.
- 85 managed + 34 host checks pass, including partial/full/invalid/PvE gauge, consumed LB, empty/expired snapshot, maximum count including center, unreachable centers and zero execution attempts during selection. Outside-PvP tests remain green.
- These are deterministic tests/implementation bounds, not live CPU or game acceptance measurements. Native gauge values, numeric positioning, collision geometry and actual hit count remain unverified in game.

## 1.0.0.4 explicit Zantetsuken command

- Current game Action sheet: 29537 = PvP Zantetsuken, Range 20, EffectRange 5, CastType 2. Official Samurai job guide agrees on target-centered 5m AoE and 20m range.
- Inspected upstream Dalamud ITargetManager and FFXIVClientStructs ActionManager declarations. Uses Target setter, CanUseActionOnTarget, GetActionInRangeOrLoS, GetActionStatus and UseAction with Macro mode (no queue). Builds against installed host bindings. No copied upstream code or shipped host binaries.
- Command runs only on explicit invocation, framework thread, logged-in live SAM in PvP excluding Wolves' Den. It reads a fresh maximum 200-player snapshot, up to 200 status lists, maximum 40,000 pair distances, 200 availability evaluations plus final revalidation. No work is added to idle frames. Repeated calls within 250ms and dispatch delayed over 500ms are dropped.
- 74 managed tests and 34 host tests pass. New checks cover inactive zero reads/actions, maximum coverage, blocked centers, ties, other source exclusion, 5m boundaries, vertical separation, failed action with no retry, empty status snapshot, 200-candidate cap, command registration, PvP gating and handler disposal.
- Coverage uses 3D center distances, not exact server hit geometry. Native calls and actual macro execution cannot be exercised by managed doubles and remain unverified in game. Native API compatibility beyond the installed host is not guaranteed.
- Source review: no packet send/receive APIs, packet serialization, hooks, cooldown/resource/position writes, bypass modes or automatic retries. Native pointers are confined to the action adapter; normal client action calls can produce normal game traffic. This supersedes the previous read-only implementation description below.
- Framework/scan tests verify no outside-PvP monitoring regression. Command feedback/help is localized in all seven languages; 667 current source-string characters verified in all four host Noto Sans CJK regional font faces using cmap tables. Live font rendering remains unverified.

## Earlier validation — 2026-10-18

## 1.0.0.3 monitoring recovery fix

- Confirmed code defect: reconciliation required LocalPlayer to exist and be SAM before subscribing; Update also permanently unsubscribed when LocalPlayer temporarily disappeared. No subsequent event was guaranteed to restart it.
- Reproduced against the previous implementation using the actual Plugin and nullable public-service doubles: failed with `PvP entry with missing player must keep recovery callback`. After the fix, entry without a player, player disappearance/reappearance, and temporarily unavailable job resume enemy enumeration without another client event.
- Monitoring lifecycle now depends only on enabled settings/login/PvP. Player validity and SAM job are per-frame scan/render gates, clearing the cache and skipping enemy reads while unavailable. No new task, timer, network access or outside-PvP polling.
- 31 host lifecycle checks and 64 managed checks pass. After recovery and PvP exit, 600 host ticks cause zero player reads and there are zero update/overlay subscriptions. Other jobs have zero enemy/status enumeration; a local player readiness check remains active only inside PvP.
- Local logs confirm the reported release loaded and a PvP entry occurred, without a logged overlay exception. They do not record LocalPlayer availability; attribution of the user's exact match failure to this defect remains unverified. No claim of live-game acceptance.

## 1.0.0.2 diagnostic release

- Local game data confirms Status 3202 is Kuzushi. This does not establish the cause of the reported detection failure.
- Added cumulative detection, source-match, invalid-duration, draw-request and projection counters. Uses the existing status iteration; no extra scan, network, persistence or game action. Counters include repeat observations.
- Preserved original selection logic. An unverified additional Hostile filter was removed before publication; no detection fix is claimed.
- 64 managed checks and 25 real-Plugin host lifecycle checks pass. Diagnostic counters are not covered by these existing tests; live verification remains pending.
- Package uses six explicit entries and a UTF-8 manifest; Windows PowerShell 5.1 compression assemblies are loaded explicitly.


## 1.0.0.1 installation fix

- User's Dalamud log confirmed `InvalidOperationException: Not on main thread!` from `ObjectTable.LocalPlayer` during the 1.0.0.0 constructor. Download/extraction succeeded; plugin construction failed and the installer subsequently removed the DLL.
- Reconciliation now runs through public `IFramework.RunOnFrameworkThread`. Constructor initialization never reads the object table from the installer worker. Inactive gates short-circuit before any player access. Queued work checks disposal and observes/logs exceptions.
- 25 new host lifecycle regression checks invoke the actual Plugin constructor on a worker with thread-enforcing service doubles. Cover loading inside/outside PvP, one-shot dispatch, no inactive player reads or persistent subscriptions, duplicate events, PvP exit, full unsubscription, disposal before dispatch and deferred errors. All passed, along with 64 existing tests.
- This verifies the logged threading failure offline, not a successful install inside the live game. An FFXIV restart clears callbacks from any failed 1.0.0.0 construction before reinstalling/updating.
- Build source paths are mapped to `/_/KuzushiBeacon` so diagnostic symbols do not embed developer filesystem paths.

## Automated verification

- Release on .NET SDK 10.0.401 / Dalamud.NET.Sdk 15.0.0, installed API 15 host references.
- 64 checks: PvP/job/toggle gates; own-source versus another samurai; status identity and lifetime; invalid IDs/durations; dead/untargetable/out-of-range/nonfinite positions; expiration/dispel; ownership transitions; bounded cache; scan throttle; seven-language completeness, normalization, initial fallback and persistence; independent visibility/color/alpha settings; invalid-config clamping.
- 196 strings across seven languages. All 580 required codepoints are present in the host's Noto Sans CJK Regular collection. Verified font order is JP=0, KR=1, SC=2, TC=3; the plugin uses those regional faces. No font files are shipped.
- Canonical StandardPluginTabs.cs is copied byte-for-byte. The settings, language settings and Support tabs use stable IDs and the original colors, spacing and click-only support behavior.
- Original 512×512 PNG icon visually inspected. No third-party or game artwork.
- Source inspection: no packet/network APIs, native pointers, hooks, signature scanners, memory writes, action/target/marker commands, HTTP requests, telemetry or extra plugin dependencies. Runtime external navigation consists only of the canonical click-triggered Ko-fi link.
- Source package excludes local paths, personal data, credentials, build intermediates and host DLLs. Release ZIP uses six explicit allowlisted entries: own DLL, deps manifest, plugin manifest, LICENSE, NOTICE and icon.

## Access and performance budget

These are deterministic managed simulations, **not measurements inside FFXIV**. Status-list iteration cost and native-wrapper overhead remain unmeasured.

| Scenario | Target | Simulation result / implementation evidence |
| --- | --- | --- |
| Outside PvP, UI closed | 0 scans; 0 status reads; no update/draw subscriptions | 36,000 inactive calls (10 minutes at 60 FPS): 0 reads. Plugin unsubscribes on exit/disable; other jobs inside PvP skip enemy/status reads. |
| 72 players, 100 ms checks | ≤10 scans/s; ≤720 status-list reads/s | 6,000 frames over 10 simulated seconds: 100 scans, 7,200 candidates, 7,200 status-list reads. Managed fake-world total elapsed 0.303 ms on this host; not a game CPU estimate. |
| All candidates | ≤200 candidates/cache entries | 500 supplied candidates: 200 status reads, 200 cached targets. |
| Repeated frame within interval | 0 additional status scans | 99 calls inside the first 100 ms produce no additional scan. |
| Config view/tab navigation | 0 saves without edits | Read-only UI; saves originate only from changed widgets/language or one-time language initialization. |
| Settings drag | Coalesce writes | 600 ms debounce; flush on closing/unload; language persists immediately. |
| Game scan/draw | 0 DB writes; 0 network requests; 0 transferred bytes | No DB/network client exists in the plugin. All reads are local public host API reads. |

The target cache lasts at most the configured interval plus 50 ms, or the remaining debuff lifetime, whichever is shorter. Maximum interval is 500 ms. Refresh replaces only this small in-memory list. Transition, disable, fault or ownership change clears it. No character IDs/names or target data are persisted. Removal detection may lag one check interval. Position reads happen only for selected targets during rendering.

## Required live acceptance (not yet performed)

- In Crystalline Conflict, Frontline and Rival Wings: own Chiten-generated Kuzushi matches the expected enemy; another samurai's debuff never matches. Verify status source behavior, including simultaneous samurai applications.
- Death/respawn, territory changes, job changes, login/logout, dispel and natural expiration correctly start/stop overlays. Wolves' Den and normal/PvE areas stay inactive.
- Line projection, camera movement, all races, configurable height, screen edges, UI scale and hidden UI. The head marker uses configurable height above feet, not a skeleton attachment. No terrain occlusion test is performed.
- Native font atlas rendering, seven-language switching and long German/French labels at supported window sizes. Glyph coverage was checked offline; rendering has not been visually verified inside the game.
- Real scan duration and FPS in crowded PvP. Settings exposes cumulative scans/candidate/status-list reads and last scan time for this purpose.

Published as a public preview with these limitations disclosed. No installed plugin DLL was replaced during development.

