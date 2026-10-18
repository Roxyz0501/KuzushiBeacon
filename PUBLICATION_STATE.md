# Publication state

## 2026-10-18 — Kuzushi Beacon 1.0.0.3 monitoring recovery fix

- Release: https://github.com/Roxyz0501/KuzushiBeacon/releases/tag/v1.0.0.3
- ZIP: https://github.com/Roxyz0501/KuzushiBeacon/releases/download/v1.0.0.3/KuzushiBeacon-1.0.0.3.zip
- Source/tag: 7634ddd9f972642daa8c0018c574b8c44f6950a1. ZIP: 57,295 bytes; SHA-256 2F8331C19C18B5F96628B17A3D3C62B5F2B379008E637E08A8C085374924A323. Anonymous download hash matches; six entries and manifest 1.0.0.3 verified. Public repository HTTP 200 and icon image/png.
- Shared index: a01264b1201e2b001a923ca1825b7a8567a20d29. Connector and anonymous commit-pinned index verified; other ten entries unchanged. Normal raw main URL initially cached 1.0.0.2.
- Confirmed/reproduced defect: temporary LocalPlayer absence prevented subscription or permanently stopped it. PvP lifecycle and player/job scan eligibility are now separate, allowing recovery without another event. No enemy/status reads while unavailable or on another job; no update/draw subscriptions outside PvP.
- 64 managed + 31 real-Plugin host checks pass; Release build zero warnings/errors. Old implementation failed the new entry-with-missing-player test. Live detection and attribution of the exact reported in-game failure remain unverified.
- No packet generation/transmission/modification, network hooks or game writes/actions. Installed files were not changed locally; public release and shared index are updated.


## 2026-10-18 — 1.0.0.2 diagnostic release published

- Version 1.0.0.2 adds detection/source/duration/render diagnostics. No confirmed detection fix is claimed.
- Original target matching retained; no unverified Hostile gate.
- Installed plugin files have not been changed. Publish the release ZIP and shared repository index before considering distribution complete.
- Public release: https://github.com/Roxyz0501/KuzushiBeacon/releases/tag/v1.0.0.2
- Public ZIP: https://github.com/Roxyz0501/KuzushiBeacon/releases/download/v1.0.0.2/KuzushiBeacon-1.0.0.2.zip
- Source/tag commit: a9154483e6c3bf29eaa8a724bd9f9860a9171eaf.
- ZIP: 57,301 bytes; SHA-256 5F5425577230453A76E4308DF16B314025CB99EF34834DC865A2721DB7D3CC9A. Anonymous download hash matches; public repository and PNG icon return HTTP 200.
- Shared index commit: 3033056a370038f7c85e5503092fa6101fc2695e. Connector confirms version/URLs and all other ten entries unchanged. Main raw URL initially returned stale CDN content.
- Validation: Release build zero warnings/errors; existing 64 managed and 25 host checks pass. Live detection remains unverified.
- GitHub CLI authentication succeeds with network access; earlier sandbox-only authentication failure did not mean the credential had expired.



## 2026-10-18 — Kuzushi Beacon 1.0.0.1 installation fix

- Release: https://github.com/Roxyz0501/KuzushiBeacon/releases/tag/v1.0.0.1
- ZIP: https://github.com/Roxyz0501/KuzushiBeacon/releases/download/v1.0.0.1/KuzushiBeacon-1.0.0.1.zip
- Source/tag commit: `04c8f20a54ebbde45a98159d08e3ebc59adfe773`.
- ZIP: 56,332 bytes; SHA-256 `F91995785FA1B9F05489EF60784A53A74DF0688924F32955E526A6559C2FA43B`.
- User log confirmed 1.0.0.0 constructor failure: ObjectTable.LocalPlayer was accessed from the installer worker, producing Not on main thread. Now dispatches reconciliation through IFramework.RunOnFrameworkThread; inactive gates skip ObjectTable entirely. Queued work respects disposal and observes failures. No packet/game-action behavior was added.
- Clean Release: zero warnings/errors. 25 new host lifecycle checks invoke the actual Plugin constructor on a worker with thread-enforcing service doubles; 64 prior checks also pass.
- Anonymous repository, PNG icon and release ZIP HTTP 200; downloaded hash matches. Six allowlisted package entries and manifest version/URLs verified. Game installation/overlay acceptance is still unverified; restart clears failed 1.0.0.0 callbacks before update/reinstall.
- Shared index commit: `92319fdad0225f03098d10660decc3ec1470724d`. Connector and anonymous commit-pinned URL verified version 1.0.0.1, preserving all other entries. Normal main URL may lag due to CDN caching.


## 2026-10-18 — Kuzushi Beacon 1.0.0.0 public preview

- Published at the user's explicit request. Initial classification: new standalone plugin; author Roxyz0501; Apache-2.0; Dalamud API 15.
- Repository: https://github.com/Roxyz0501/KuzushiBeacon
- Release: https://github.com/Roxyz0501/KuzushiBeacon/releases/tag/v1.0.0.0
- ZIP: https://github.com/Roxyz0501/KuzushiBeacon/releases/download/v1.0.0.0/KuzushiBeacon-1.0.0.0.zip
- Source/tag commit: `d2300e54bfaa06c5209ebcd669b7218000a3b5d5`.
- ZIP: 55,209 bytes; SHA-256 `13E8F6647A70CE23E3CA56FAD0DF8C795DC733866575C55B9EDD1D59A7914A7B`.
- Local line and overhead marker for the player's own Kuzushi. Independent visibility/colors/alpha, line width, marker size/height, distance and scan interval. No packet/network hooks, game writes or action/target/marker execution. Event-driven suspension outside PvP/on other jobs; default 100 ms scan interval.
- Clean Release zero warnings/errors; 64 managed checks; seven languages, 196 strings, 580 required font codepoints verified. Original 512x512 icon and byte-identical shared tabs. Six allowlisted release entries; author/version/RepoUrl/IconUrl retained.
- Anonymous repository, PNG icon and ZIP HTTP 200; public ZIP hash matched. Native game overlay placement, real status-source behavior and game performance remain unverified and are disclosed in the public preview.
- Shared index registration: `a553a904e21e0fb1475df8f0c2e0754d9cbd0bb8`. Connector and anonymous commit-pinned index verified 10 entries, preserving all 9 existing entries. Normal main URL initially returned a cached 9-entry index.
- Source/icon/index/ledger writes used the connected GitHub integration. CLI used for unsupported repository creation, Release/asset publishing and local Git-object synchronization.
