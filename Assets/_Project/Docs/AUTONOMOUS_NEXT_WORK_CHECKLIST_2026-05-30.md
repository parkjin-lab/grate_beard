# Autonomous Next Work Checklist - 2026-05-30

## Current Progress
- Core loop rhythm has a clear target shape: Calm -> Build -> Spike -> Release.
- Runtime pressure, threat readability, escape relief, risk rewards, exit choice carryover, semantic stingers, and Korean-first HUD copy are implemented.
- Debug validation now shows rhythm phase observation and semantic stinger telemetry.
- Large vendor packages are ignored until license, scope, and repository size are approved.

## Latest Code Review Notes
| Area | Finding | Action |
| --- | --- | --- |
| Rhythm validation | Phase observation is intentionally lightweight and non-persistent | Keep as debug-only; minimize human play validation and rely on snapshot/static evidence first |
| Semantic stinger telemetry | Before the first stinger, age could be displayed as a negative debug value | Fixed by adding `HasRuntimeStingerTelemetry` and a `none` display path |
| Semantic stinger authoring | Only a small subset of stingers had authored clip slots | Expanded optional stinger clip slots so semantic tones can be replaced one by one |
| Placeholder stinger rhythm | Several generated tones were functional but too similar in emotional shape | Tuned warning/chase/relief/breath/rhythm placeholders to make each cue easier to distinguish |
| Stinger validation access | Only exit and chase stingers had direct context-menu test hooks | Added context-menu test hooks for the main semantic stinger set |
| Rhythm test evidence | Human play checks should be minimized | Added `F8`/`Write Rhythm Snapshot` to DebugOverlay so rare checks produce reusable telemetry under `Logs/RhythmValidation/` |
| Rhythm phase coverage | Testers still had to infer which phases were missing from C/B/S/R flags | Added explicit missing-phase labels to the overlay and snapshot file |
| Low-touch guardrails | Validation helpers could regress silently during later UI/audio edits | Added static preflight checks for rhythm snapshot/missing-phase hooks and semantic stinger test hooks |
| Spawn safety guardrails | Player/monster spawn safety should not rely on repeated player validation | Added static preflight coverage for player unsafe-position recovery and enemy narrow-spawn avoidance hooks |
| Rhythm state guardrails | Rhythm phase order and pressure modulation should not rely on human observation | Added static preflight coverage for Calm->Build->Spike->Release transitions, spike tell, release relief, and regression suppression |
| Vendor asset guardrails | Large Asset Store imports should not re-enter source control accidentally | Added static preflight coverage for ignored vendor package paths |
| Validation artifact guardrails | Snapshot/log artifacts should remain local evidence, not source-controlled data | Added static preflight coverage for log artifact ignore rules |
| Semantic stinger slot guardrails | Authored SFX replacement should not silently lose cue slots | Expanded static preflight coverage to include all semantic stinger clip slots, resolver, count, and audition hooks |
| Performance | Stinger telemetry stores primitive fields only when a stinger plays | No per-frame allocation concern |
| Risk | Some feel/audio judgment still needs a human eventually | Defer broad play validation; prioritize automated/static guards and tiny evidence captures |

## Human-Free Implementation Queue
1. Keep debug/validation displays readable and non-invasive.
2. Add or refine documents that convert design goals into testable acceptance criteria.
3. Improve generated placeholder feedback only when it does not require new licensed assets. `[done: semantic stinger clip slots and placeholder rhythm pass]`
4. Keep vendor assets ignored until explicitly approved.
5. Run static preflight after each code change and commit only scoped files.

## Next Priority
The next best autonomous task is to reduce the need for human play validation:
- Prefer static/preflight checks, deterministic runtime counters, and debug snapshots over repeated manual runs.
- Add small guardrails that catch missing rhythm phases, missing stinger assignments, spawn safety regressions, invalid state transitions, vendor import regressions, and validation artifact leakage. `[in progress: validation, spawn safety, rhythm state, vendor ignore, log artifact, and stinger slot preflight checks added]`
- Use authored SFX/art only after resource ownership is confirmed.
- Keep any human pass short and evidence-producing: one snapshot per meaningful anomaly is enough.

## Longer-Term Quality Ideas
- Author a small bespoke horror motif kit before relying on large UI/vendor packages.
- Replace generated stinger tones one semantic group at a time.
- Add one set-piece per rhythm phase instead of adding difficulty only through numbers.
- Build a capture pack showing quiet, lure, spike, chase, relief, and exit choice.
