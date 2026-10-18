# Validation — 2026-10-19

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

