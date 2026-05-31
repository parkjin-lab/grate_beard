# Autonomous Next Work Checklist - 2026-05-30

## Current Progress
- Core loop rhythm has a clear target shape: Calm -> Build -> Spike -> Release.
- Runtime pressure, threat readability, escape relief, risk rewards, exit choice carryover, semantic stingers, and Korean-first HUD copy are implemented.
- Debug validation now shows rhythm phase observation and semantic stinger telemetry.
- Large vendor packages are ignored until license, scope, and repository size are approved.

## Latest Code Review Notes
| Area | Finding | Action |
| --- | --- | --- |
| Rhythm validation | Phase observation is intentionally lightweight and non-persistent | Keep as debug-only; use manual notes for feel quality |
| Semantic stinger telemetry | Before the first stinger, age could be displayed as a negative debug value | Fixed by adding `HasRuntimeStingerTelemetry` and a `none` display path |
| Semantic stinger authoring | Only a small subset of stingers had authored clip slots | Expanded optional stinger clip slots so semantic tones can be replaced one by one |
| Placeholder stinger rhythm | Several generated tones were functional but too similar in emotional shape | Tuned warning/chase/relief/breath/rhythm placeholders to make each cue easier to distinguish |
| Stinger validation access | Only exit and chase stingers had direct context-menu test hooks | Added context-menu test hooks for the main semantic stinger set |
| Rhythm test evidence | Manual rhythm tests needed an easy way to preserve current telemetry | Added `F8`/`Write Rhythm Snapshot` to DebugOverlay, saving phase/pressure/audio/player state under `Logs/RhythmValidation/` |
| Performance | Stinger telemetry stores primitive fields only when a stinger plays | No per-frame allocation concern |
| Risk | Full Unity Play Mode validation is still needed for actual feel and audio mix | Prioritize a 10-minute rhythm pass when editor access is available |

## Human-Free Implementation Queue
1. Keep debug/validation displays readable and non-invasive.
2. Add or refine documents that convert design goals into testable acceptance criteria.
3. Improve generated placeholder feedback only when it does not require new licensed assets. `[done: semantic stinger clip slots and placeholder rhythm pass]`
4. Keep vendor assets ignored until explicitly approved.
5. Run static preflight after each code change and commit only scoped files.

## Next Priority
The next best autonomous task is to tighten validation around the 10-minute rhythm pass:
- Make sure the debug overlay reports what the tester needs without opening extra panels.
- Keep manual validation focused on whether each rhythm phase changes player decisions.
- Use authored SFX/art only after resource ownership is confirmed.
- Use `AudioManager` context-menu stinger tests before Play Mode rhythm passes to confirm cue contrast.
- Capture at least one rhythm snapshot per Calm/Build/Spike/Release pass.

## Longer-Term Quality Ideas
- Author a small bespoke horror motif kit before relying on large UI/vendor packages.
- Replace generated stinger tones one semantic group at a time.
- Add one set-piece per rhythm phase instead of adding difficulty only through numbers.
- Build a capture pack showing quiet, lure, spike, chase, relief, and exit choice.
