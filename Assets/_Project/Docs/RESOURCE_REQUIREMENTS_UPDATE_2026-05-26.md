# Resource Requirements Update - 2026-05-26

## Current Context
- Recent gameplay work added release relief, risk-reward temptation, spike tells, phase-aware exit carryover, semantic stingers, and Korean-first player-facing UI copy.
- The next design need is a stronger horror rhythm: readable quiet, controlled pressure rise, authored spike, and believable relief.
- The previous resource requirements document remains in the project, but this update is the current clean checklist for production decisions.

## Implemented This Pass
- `DreadScreenOverlayRuntime` now supports a subtle procedural edge-scratch motif inside the dread vignette texture.
- The motif is generated once with the runtime texture, so it adds horror identity without per-frame allocations or extra art dependencies.
- Tunable fields were added for scratch enablement, strength, edge bias, and count.
- Vendor asset folders were classified and protected from accidental commits while license, size, and scope are reviewed.
- `DebugOverlay` now includes a lightweight rhythm validation gate for Calm/Build/Spike/Release observation.
- `DebugOverlay` now exposes the last semantic stinger, source, age, volume, pitch, and suppression count for audio rhythm validation.
- `AudioManager` now has optional authored clip slots for the semantic stinger set, so placeholder tones can be replaced one cue at a time.
- `DebugOverlay` can now write rhythm validation snapshots to `Logs/RhythmValidation/` for rare, low-touch Play Mode checks.
- Static preflight now checks that local text and JSON summary output hooks remain present for automation-readable evidence.
- Static preflight now parses the generated JSON summary after writing it and fails if summary counts or result counts do not round-trip.
- Static preflight JSON summaries now include schemaVersion, exitCode, hasFailures, and hasWarnings for automation-readable status.
- Static preflight JSON summaries now include optional Git branch, commit, dirty, and status-count metadata for traceable validation evidence.
- Static preflight summaries now include durationMilliseconds so automation can notice unusually slow static checks.
- Static preflight summaries now include a 60s duration warning threshold and durationWarning flag without turning slow runs into failures.
- Static preflight now checks that low-touch rhythm snapshot hooks and semantic stinger test hooks remain present.
- Static preflight now checks that player unsafe-position recovery and enemy narrow-spawn avoidance hooks remain present.
- Static preflight now checks that rhythm state transitions, rhythm pressure modulation, spike tell, release relief, and regression suppression hooks remain present.
- Static preflight now checks that Release relief keeps multiple non-text channels: stamina, fog reveal, pulse, objective whisper, audio, calm window, quiet breath, and semantic telemetry.
- Static preflight now checks that ignored vendor Asset Store import paths remain protected in `.gitignore`.
- Static preflight now checks that ignored vendor Asset Store import paths are not already tracked by Git.
- Static preflight now checks that logs and validation artifacts remain ignored instead of entering source control.
- Static preflight now checks that semantic stinger clip slots, resolver, counter, and audition hooks remain present.
- Static preflight now checks that low-touch planning/resource/rhythm/vendor analysis documents remain present.
- Static preflight now checks that required planning documents keep their Unity `.meta` files.
- Static preflight now includes rhythm handoff and project status update documents in the required planning artifact set.
- Static preflight now checks that the rhythm validation playbook preserves the minimal-human-pass and snapshot evidence policy.
- Static preflight now labels stale release-soak logs with freshness days and refresh-required status so old evidence is not mistaken for current validation.

## P0 Resource Needs
| Area | Needed Resources | Why It Matters | Acceptance Criteria |
| --- | --- | --- | --- |
| Visual motif kit | 6-10 authored references for enemy tell shapes, route echo, corruption, relief, death overlay, and exit lure | Runtime systems now have readable states, but they need a consistent horror identity | Every high-priority cue can be recognized in a screenshot without relying only on text |
| Semantic SFX pack | Authored clips for lock-on warning, chase start, escape relief, quiet breath break, echo return, risk reward, rhythm shift, set-piece shift, exit unlock, and death | The audio manager has semantic roles; placeholder tones should become intentional horror rhythm | Each semantic event has a distinct attack, tail length, and mix priority |
| Low-touch validation set | Static/preflight reports, 10-minute release soak script, death-respawn flashlight check, monster spawn/stuck checklist, rhythm snapshot notes | Recent fixes touched pacing, respawn, camera depth, and enemy readability | Human play checks are minimized; when used, they produce reusable snapshots instead of subjective-only notes |
| Vendor asset decision | License/source/size review for `Assets/Feel`, `Assets/Layer Lab`, and `Assets/ThirdParty.meta` | These folders are present but intentionally ignored until approved; they may affect repository weight and license posture | Each folder is either approved for a scoped commit, kept local only, or replaced with smaller authored resources |

## P1 Resource Needs
| Area | Needed Resources | Why It Matters | Acceptance Criteria |
| --- | --- | --- | --- |
| Character and monster sprite polish | Player idle/walk/panic frames, monster idle/search/chase/stun frames | Readability is improving mechanically; animation should now express state rhythm | Player and monster state changes are visible at gameplay zoom |
| Korean HUD icon set | Small icons for stamina, objective, danger, echo, decoy, smoke, flashlight, exit, and breadcrumb | Korean copy is now player-facing; icons can reduce text density during high pressure | Icons remain legible at 1080p and do not compete with threat cues |
| Map prop silhouettes | Narrow passage blockers, broken lights, exit frame, safe haven anchor, hook/bait prop | Core loop needs spaces that imply choices before text explains them | Props communicate risk, relief, or temptation by silhouette |
| Steam/store capture pass | 5-8 screenshots after visual motif and SFX pass | External presentation should wait until the game has a recognizable look | Captures show quiet, build, spike, chase, relief, and exit choice moments |

## Authoring Direction
- Quiet phase resources should be sparse, low-contrast, and slow. They should make the player listen and scan.
- Build phase resources should add directional hints and uncertain temptation: a visible reward should also imply exposure.
- Spike phase resources should be short, bright, and unmistakable. The cue should feel sudden, but the player must understand what happened.
- Release phase resources should briefly clear visual/audio pressure so the player can feel the rhythm reset.

## Immediate Next Work Without Planner Input
1. Run Unity compile/static checks after the dread motif change.
2. Decide whether the ignored vendor folders should stay local-only or be re-imported as smaller scoped packages.
3. Minimize human Play Mode validation; use debug rhythm snapshots only for anomalies or final spot checks.
4. Replace one placeholder semantic SFX group at a time, starting with `ChaseStarted`, `LockOnWarning`, and `EscapeRelief`.

## Definition Of Done For This Resource Slice
- The resource list is visible in source control as the current production checklist.
- Runtime horror identity has one concrete visual improvement that does not require new art.
- No unreviewed vendor asset folder is accidentally committed.
- The next content pass can proceed asset-by-asset instead of reopening the whole design discussion.
