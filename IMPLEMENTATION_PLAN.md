# PvP Supporter expansion (2026-10-19)

Requested scope: 2, 4, 5, 8, 9; 16–75; 77–100 and PvP Ledger integration.

The original plugin identity, aliases, saved settings, common language/support tabs and normal client action path are retained. No packet generation or transmission hooks. New targeting is explicit-command-only. All optional features have individual switches; notification/audio and genre masters are independent. LB geometry remains gated by a full gauge. General AoE analysis is separately opt-in. Safety revalidation cannot be disabled by performance options.

## Delivery checklist

- [x] Feature catalog, bounded shared snapshots, configuration migration and category tabs
- [x] Fixed/job-based range commands, nearest/ally/assist/target-only commands, status exclusions, dry run
- [x] Attack opportunities 16–30
- [x] Samurai 31–45
- [x] Survival 46–60
- [x] Allies/positioning 61–75
- [x] Notifications/display 77–85, master controls and local audio volume
- [x] Anonymous match review 86–95
- [x] Resource budgets, expiry and metrics 96–100
- [x] PvP Ledger KDA/feed, history/CSV and optional Discord integrated; licenses retained
- [x] Regression/feature tests, build, package inspection
- [x] Public source/release/shared index verification (not local-only)

## Evidence / limits

Local game data inspected 2026-10-19: Guard 3054/3673; Kuzushi 3202; Chiten 1240; Resilience 3248; Undead Redemption 3039. Actions: Guard 29054; Purify 29056; Elixir 29055; Recuperate 29711 (2000 MP). Status exclusions use an explicit versioned list, not inferred enemy cooldowns. Offscreen objects may be selectable; screen-only is an independent projection/viewport condition. Range is hitbox-adjusted except target-centered AoE geometry. Spatial predictions never guarantee actual damage or a safe route.

PvP Ledger source: ../PvP Ledger - PvP戦績表示・記録プラグイン/PvPLedger, published v1.0.0.0. Its result hooks observe inbound results only, with exact game-build gating and original callbacks preserved. Discord defaults off; no test delivery is authorized by implementation alone.
