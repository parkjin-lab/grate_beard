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
| Machine-readable preflight summary | Automation needs stable report files without reading terminal output | Added static preflight coverage for local text and JSON summary output hooks |
| Preflight JSON readback | Automation should fail fast if a summary file is malformed | Added post-write JSON parse/count verification to the static preflight script |
| Preflight JSON contract | Future automation should not infer schema or exit semantics | Added schemaVersion, exitCode, hasFailures, and hasWarnings to the JSON summary and readback check |
| Preflight Git context | Validation evidence should identify the branch and commit it came from | Added optional Git branch, commit, dirty, and status-count metadata to the JSON summary |
| Preflight duration evidence | Automation should notice if the static gate becomes unexpectedly slow | Added durationMilliseconds to text and JSON preflight summaries |
| Preflight duration warning | Slow static checks should be visible without becoming release failures | Added a 60s duration warning threshold and JSON/text warning flag |
| Preflight conflict scan performance | Ignored vendor folders should not slow source-control hygiene checks | Excluded ignored Feel, Layer Lab, and recovery folders from conflict marker scans |
| Spawn safety guardrails | Player/monster spawn safety should not rely on repeated player validation | Added static preflight coverage for player unsafe-position recovery and enemy narrow-spawn avoidance hooks |
| Player wall-spawn recovery | Player can still appear inside existing scene blockers, not only generated map blockers | Defaulted player spawn safety to all blocker colliders, added generated-cell-center fallback recovery, immediately re-runs player safety recovery after map placement, and sizes spawn clearance from the player collider plus padding |
| Rhythm state guardrails | Rhythm phase order and pressure modulation should not rely on human observation | Added static preflight coverage for Calm->Build->Spike->Release transitions, spike tell, release relief, and regression suppression |
| Spike fairness cue budget guardrails | Spike should be telegraphed and not stack major cues unfairly | Added static preflight coverage for Build-phase LockOnWarning tells, major stinger budget, chase/lock-on stingers, and duck boost hooks |
| Build temptation wager guardrails | Build should make risk caches and breadcrumb momentum tempting, not static | Added static preflight coverage for phase-aware risk cache reward/noise and breadcrumb momentum reward/chain hooks |
| Release relief contract guardrails | Release must be felt through multiple non-text channels | Added static preflight coverage for stamina, fog reveal, pulse, whisper, audio, calm window, quiet breath, and semantic event hooks |
| Rhythm set-piece alignment | Set-pieces should land as Build crest or Spike entry beats, not only stage-number events | Added rhythm-aware set-piece delay/tuning, BuildCrest/SpikeEntry labels, debug overlay readout, rhythm snapshot evidence, and static preflight hooks |
| Vendor asset guardrails | Large Asset Store imports should not re-enter source control accidentally | Added static preflight coverage for ignored vendor package paths |
| Vendor tracking guardrails | Ignored vendor packages could still be committed if already tracked | Added static preflight coverage for tracked vendor package paths |
| Validation artifact guardrails | Snapshot/log artifacts should remain local evidence, not source-controlled data | Added static preflight coverage for log artifact ignore rules |
| Semantic stinger slot guardrails | Authored SFX replacement should not silently lose cue slots | Expanded static preflight coverage to include all semantic stinger clip slots, resolver, count, and audition hooks |
| Planning artifact guardrails | Low-touch work depends on current planning/resource/rhythm/vendor docs staying available | Added static preflight coverage for required project planning artifacts |
| Planning meta guardrails | Unity docs need stable `.meta` files to avoid asset GUID churn | Added static preflight coverage for required planning artifact `.meta` files |
| Handoff/status artifact guardrails | Future agents need the last rhythm handoff and project status trail | Expanded static preflight planning coverage to require rhythm handoff and project status update docs |
| Low-touch validation policy guardrails | Automation should not drift back into broad manual play checks | Added static preflight coverage for the rhythm playbook's minimal-human-pass and snapshot evidence policy |
| Rhythm judgment card | Human spot checks need plain terms for fair Spike, felt Release, and tempting Build | Added a Korean-first judgment card to the rhythm playbook and static preflight policy hooks |
| Stale log warning clarity | Old release-soak logs should not be mistaken for current validation | Static preflight now labels stale log warnings with freshness days and refresh-required status |
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
- Add small guardrails that catch missing rhythm phases, malformed machine-readable summaries, missing stinger assignments, spawn safety regressions, invalid state transitions, spike fairness regressions, build temptation regressions, release relief contract regressions, rhythm set-piece alignment regressions, vendor import regressions, validation artifact leakage, missing planning/handoff artifacts, and validation-policy drift. `[in progress: validation, machine summary/readback/schema/git context/duration warning, conflict-scan performance, player wall-spawn recovery, spawn safety, rhythm state, spike fairness, build temptation, release relief, rhythm set-piece alignment, vendor ignore/tracking, log artifact, stinger slot, planning/handoff artifact, and low-touch validation policy preflight checks added]`
- Keep human rhythm notes plain: `scary but fair`, `unfair`, `felt relief`, `no relief`, `tempted`, or `flat` is enough.
- Treat stale release-soak log warnings as refresh signals, not release-blocking failures, unless a fresh build claim depends on those logs.
- Use authored SFX/art only after resource ownership is confirmed.
- Keep any human pass short and evidence-producing: one snapshot per meaningful anomaly is enough.

## Longer-Term Quality Ideas
- Author a small bespoke horror motif kit before relying on large UI/vendor packages.
- Replace generated stinger tones one semantic group at a time.
- Add one set-piece per rhythm phase instead of adding difficulty only through numbers.
- Build a capture pack showing quiet, lure, spike, chase, relief, and exit choice.
