# PvP Supporter persistence audit implementation — 2026-10-22

Scope: existing PvP Supporter repository only. Parent PERSISTENCE_SPEC.md and StandardHistoryFilesTab.cs adopted; shared specs/templates were not edited. No production-data migration was executed and no installed DLL was replaced.

## Configuration

- UI applies changes immediately and increments a revision; one lightweight framework scheduler checks every 60 seconds. Only dirty revisions serialize. Equal/reverted snapshots write nothing. No SavePluginConfig calls remain, including embedded Ledger settings and language changes.
- Immutable snapshots use one background writer. Logout/Dispose waits for the writer and flushes newer changes. Clean shutdown removes the scheduler. Forced termination can lose up to the unsaved interval; it does not guarantee a flush.
- UTF-8 temporary file is durably flushed and validated before atomic replacement. Replacement preserves the prior valid primary under a uniquely ordered backup name; only after success are valid prior generations capped at three. Corrupt primaries go to a distinct retained `.corrupt-*` path, never a normal generation.
- Startup prefers a valid primary, otherwise the newest valid backup. Recovery is reported. No backup means defaults plus an error notice and preservation of damaged data on subsequent replacement. Settings validation limits input to 8 MiB and requires an integer Version and typed deserialization.
- Three failed scheduled saves stop automatic attempts; dirty snapshot remains. Settings has explicit retry and error detail. Failed replacement never rotates backups. Startup recovery errors retain the recovered snapshot for retry.

## History and bounded work

- Ledger: owner/yyyyMMdd/{original UTC-start timestamp and match GUID}.match.json plus summary/delivery. Still one match per transaction, max 1 MiB body, 1,000 matches / 128 MiB / 365 days per owner. Flat legacy files remain readable and retain delivery claims; no bulk relocation is required. All-day inventory is metadata-only, pages are 25 summaries with four-page cache; detail reads one record.
- Reviews: UTC-start date directories, one record per completed/interrupted match, GUID plus normalized-content hash identity. Midnight/late events keep the originating match partition. Max 100 records / 4 MiB total and strict 4 MiB record/export limit. UI pages expose all retained records, not only the latest 20.
- Legacy matches.json is split once into verified idempotent records; a completion marker is written after successful records. Original file remains. A partial migration resumes without rewriting matching records; invalid records/source remain available for investigation. New matches do not rewrite old history.
- Anonymous exports remain explicit, max 20 records per export and 20 export files. Ledger CSV exports remain explicit and are not silently deleted.
- Review disk work has one outstanding match-boundary writer (backpressure), at most one queued export. Three failed writes stop new review collection; failed snapshots remain for explicit retry. Active match state is bounded by existing counters and 50 death snapshots. No timer periodically rewrites history.
- Ledger write channel remains bounded at 16; full-channel backpressure prevents drops. Three consecutive save failures stop new collection and retain jobs for manual retry (at most queued 16 plus active/boundary work). Durable Discord delivery claim still flushes BEFORE any POST, so unknown delivery never triggers duplicate sending.
- Ledger read retries are spaced 30 seconds and stop at three; UI offers retry. Auxiliary history/detail/export tasks are tracked and joined. Worker completions never wait for UI dispatch. Logout flushes match work; shutdown stops intake/events, cancels HTTP, drains writes, joins auxiliary work and clears completion queues.
- Filesystem failure can still prevent a final flush; errors are reported. Tests do not imply successful storage on a full/unavailable disk.

## History files tab

Independent localized HistoryFiles tab precedes the standard Settings / Language / Support tabs. Exact canonical source copy, seven languages and font coverage. Metadata measurement is single-flight and cached, refreshed on settings-window open, storage revision or explicit refresh. No file body reads / per-frame enumeration. Totals cover ledger/review roots including exports, legacy, migration markers, temporary/recovery files; configuration and unrelated cache are excluded. Directory opening is explicit and restricted to these roots. Measurement errors are not reported as zero. Worker is cancelled/joined at disposal. No browser UI exists in this plugin.

## Synthetic verification

- 367 managed checks: idle/reverted settings save zero; 100 edits coalesce to one save; five successful settings commits total 65 logical payload bytes; max simultaneous writers one; changes during a write survive flush; exactly three spaced failures then stop; explicit retry; Windows file lock; valid-backup recovery; valid primary preservation; three-generation rotation; shutdown no further scheduled write.
- Review probe: two different UTC dates, delayed order and restart; two new record commits total 112 logical payload bytes; duplicate save adds zero writes/bytes; strict oversized record rejection; resumable/idempotent legacy migration with source retained.
- 68 real-host lifecycle checks: UI edit does not write immediately, logout flushes, duplicate logout/Dispose adds zero writes, pending review joins and saves in its start date, all subscriptions removed, metadata gives one file / exactly 123 B excluding settings.
- 2,789 Ledger checks: mixed flat/date storage, midnight-crossing match, old/new delivery claims across restart. Existing page cache: first page 25 reads / 8,700 summary bytes, 500 unchanged opens zero additional reads/writes/bytes, concurrent next page +25 reads, detail +1 read. 60 match commits = 120 payload-file writes; no full-history serialization.
- 487 production headless ImGui frames, including seven HistoryFiles languages and glyph coverage across four fonts. Japanese storage tab render inspected. Canonical template byte identity checked.
- These are successful-commit payload counts and file metadata, not OS I/O tracing or SSD physical/NAND measurements. Failed temporary-file attempts may have written bytes before replacement failed; those bytes are not included in the successful-commit counters. Live game logout/unload races, native actions, audio, real disk-full behavior and SSD-level traffic remain unverified. Disk-full/replace failure was injected; actual file-lock failure was exercised with synthetic files.

No pending shared-spec differences. Exact source/tag, package hash and index commit are recorded in PUBLICATION_STATE.md after publishing.
