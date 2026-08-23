# Autonomous Next Work Checklist - 2026-05-30

## Current Progress
- Core loop rhythm has a clear target shape: Calm -> Build -> Spike -> Release.
- Runtime pressure, threat readability, escape relief, risk rewards, exit choice carryover, semantic stingers, and Korean-first HUD copy are implemented.
- Debug validation now shows rhythm phase observation and semantic stinger telemetry.
- Large vendor packages are ignored until license, scope, and repository size are approved.
- 2026-08-23: Echo Overcharge and stability 1-5 shipped. Facing flips from move input. Echo scout uses active lists. Release enter now has a one-shot lived cue (`숨이 트인다`, ambient settle, breadcrumb glow) on the existing relief hook. Walls use mossy stone art, crumbs use glowing bread, fog no longer Find-by-tag every frame, and death still wipes explored fog but recarves vision around the player on the same frame. Still open: Unity compile, Play Mode tap/hold/flip/art/respawn-vision check. Rhythm numbers untouched.

## Latest Code Review Notes
| Area | Finding | Action |
| --- | --- | --- |
| Rhythm validation | Phase observation is intentionally lightweight and non-persistent | Keep as debug-only; minimize human play validation and rely on snapshot/static evidence first |
| Semantic stinger telemetry | Before the first stinger, age could be displayed as a negative debug value | Fixed by adding `HasRuntimeStingerTelemetry` and a `none` display path |
| Semantic stinger authoring | Only a small subset of stingers had authored clip slots | Expanded optional stinger clip slots so semantic tones can be replaced one by one |
| Placeholder stinger rhythm | Several generated tones were functional but too similar in emotional shape | Tuned warning/chase/relief/breath/rhythm placeholders to make each cue easier to distinguish |
| Stinger validation access | Only exit and chase stingers had direct context-menu test hooks | Added context-menu test hooks for the main semantic stinger set |
| Rhythm test evidence | Human play checks should be minimized | Added `F13`/`Write Rhythm Snapshot` to DebugOverlay so rare checks produce reusable telemetry under `Logs/RhythmValidation/` |
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
| Build risk-cache readability | Build rewards should be visible before pickup, not only calculated after pickup | Risk caches now pulse faster/larger and lift color during Build, with static preflight hooks |
| Build breadcrumb chain readability | Breadcrumb streaks should feel like a tempting run during Build, not only a hidden stamina bonus | Build now lengthens/widens breadcrumb chain echoes, slightly enlarges momentum pulses, and broadens chain noise radius with static preflight hooks |
| Spike clutch breadcrumb reward | Spike should be scary but still feel recoverable when the player makes a clean clutch choice | Spike breadcrumb momentum can now advance the phase toward Release while reporting the applied relief time, with static preflight hooks |
| Breadcrumb reward Korean wording | Momentum reward alerts should read like player language, not debug telemetry | Breadcrumb chain reward messages now use Korean-first rhythm labels and relief timing, with static preflight hooks |
| Cache reward Korean wording | Risk/exit cache alerts should describe the choice reward in player language | Risk cache and exit choice cache reward messages now use Korean-first stamina, echo cooldown, and route-hint wording with static preflight hooks |
| Exit choice Korean wording | Exit unlock/taken/carryover alerts should be readable without English fallback text | Exit unlock, cache exposed, route carryover, and exit decision messages now use Korean-first helpers with static preflight hooks |
| Release relief contract guardrails | Release must be felt through multiple non-text channels | Added static preflight coverage for stamina, fog reveal, pulse, whisper, audio, calm window, quiet breath, and semantic event hooks |
| Release relief Korean wording | Relief recovery alerts should feel like the player's breath returning, not debug output | Escape relief and Rhythm Release recovery events now use Korean-first helper messages with static preflight hooks |
| Release camera exhale | Release should briefly feel like an exhale, not only a resource refill | Rhythm Release now opens the camera slightly and settles look-ahead/smoothing for a short fade-out window, with static preflight hooks |
| Release route clarity | Release should prepare the next choice, not only lower pressure | Rhythm Release now lengthens/widens objective whisper guidance with static preflight hooks |
| Release end tension cue | Release should end with a small warning that the rhythm is about to rise again | Release now raises a one-shot Build-returning RhythmShift cue near its end, with static preflight hooks |
| Rhythm cue Korean wording | Player-facing rhythm alerts should not leak developer English | Release-end rhythm cue now uses Korean-first `다시 빨라진다` wording in the event, priority toast, HUD alert feed, and static preflight hooks |
| Ability event Korean wording | Core ability alerts should read as player feedback, not debug counters | Echo pulse, echo return, decoy, and smoke deployment events now use Korean-first helper messages with static preflight hooks |
| Player event Korean wording | Death and over-sprinting after relief should read as player feedback, not debug telemetry | Death cause, missed option, death count, and quiet-breath break events now use Korean-first helper messages with static preflight hooks |
| Stage event Korean wording | Pressure and set-piece events should read as horror rhythm beats, not raw telemetry | Stage pressure and set-piece shift events now use Korean-first helper messages with static preflight hooks |
| System event Korean wording | Map generation, loadout, and manual echo scan events should not leak English debug labels | Stage generation, loadout unlock/lock/apply, and echo objective scan events now use Korean-first helper messages with static preflight hooks |
| Runtime event final wording sweep | Remaining rhythm, threat, safe-zone, haunted-room, and breadcrumb events should not leak English debug labels | Rhythm shift/spike tells, pressure waves, safe-haven thinning, haunted room reactions, enemy lock-on, and breadcrumb count events now use Korean-first helper messages with static preflight hooks |
| Rhythm overlay response | Spike and Release should feel different without relying only on text or audio | Dread overlay now reads the rhythm director: Spike adds pulsing edge/red pressure, while early Release eases alpha/red blend, with setup wiring and static preflight hooks |
| Rhythm set-piece alignment | Set-pieces should land as Build crest or Spike entry beats, not only stage-number events | Added rhythm-aware set-piece delay/tuning, BuildCrest/SpikeEntry labels, debug overlay readout, rhythm snapshot evidence, and static preflight hooks |
| Rhythm snapshot readability | Snapshot files should guide quick human judgment without long playtest notes | Added QuickRead/JudgmentPrompt lines and static policy hooks for the Korean judgment card |
| Spike fairness telemetry | Spike should be scary but explainable in snapshots, not only judged by feel | Rhythm now records LockOnWarning and ChaseStarted timing, reports entry/chase warning status in DebugOverlay snapshots, and has regression/static hooks |
| Release relief telemetry | Release should be felt through active relief channels, not only through copy | Release now reports calm window, camera exhale, quiet breath, objective whisper, and stamina recovery channels in DebugOverlay snapshots, regression checks, and static hooks |
| Release first tuning pass | Release relief should last long enough for the player to notice the rhythm reset | Lengthened calm window, quiet breath, camera exhale, objective whisper, and overlay relief defaults, with scene calm/quiet values updated |
| Rhythm snapshot summary tool | Automation needs to read snapshot evidence without terminal archaeology | Added `Tools\Summarize-RhythmSnapshots.cmd` to summarize phase counts, Calm readable/rushed status, Build temptation pass/flat status, Spike fairness pass/unfair status, and Release relief channel pass/weak status |
| Rhythm snapshot JSON summary | Automation should read rhythm evidence without parsing terminal text | Added `-OutputJsonPath` support to write schemaVersion, thresholds, phase statuses, counts, and weak evidence files as JSON |
| Rhythm snapshot default summary command | Heartbeats should not need to remember output paths | Added `Tools\Write-RhythmSnapshotSummary.cmd` to write `Logs\RhythmValidation\rhythm_snapshot_summary_last.json` with one command |
| Rhythm snapshot overall status | Automation should distinguish missing evidence from tuning failures without custom parsing | Added `OverallEvidenceStatus`, `overallEvidenceStatus`, and `phaseEvidenceComplete` to the snapshot summary output and JSON |
| Rhythm next-action helper | Heartbeats should choose capture, tuning, or variation work from evidence instead of guessing | Added `Tools\Get-RhythmNextAction.cmd` to read the default summary JSON and print the next autonomous action |
| Rhythm snapshot hotkey guardrail | Snapshot capture should not trigger gameplay/debug side effects while gathering evidence | Moved the rhythm snapshot hotkey from `F8` to `F13`, keeping the overlay button as the fallback path |
| Rhythm snapshot hotkey isolation | Future validation key changes should fail fast if they collide with gameplay or debug keys | Added static preflight parsing for the snapshot key against loadout, save, regression, audio, debug, and map tuning hotkeys |
| Rhythm capture handoff status | Automation should know when evidence capture needs a person instead of pretending tuning can continue | Added `requiresHumanCapture` and `automationCanProceed` to `Tools\Get-RhythmNextAction.cmd` output and JSON |
| Rhythm capture handoff steps | The next tiny human pass should be executable without reading prose docs | Added `captureHotkey`, `minimumCaptureCount`, and `humanCaptureSteps` to the next-action output and JSON |
| Rhythm next-action default JSON | Heartbeats should read the same next-action file without reconstructing command arguments | Added `Tools\Write-RhythmNextAction.cmd` to refresh the summary and write `Logs\RhythmValidation\rhythm_next_action_last.json` |
| Rhythm capture handoff note | Busy creators should get a short readable capture note without parsing JSON | Added `Tools\Write-RhythmCaptureHandoff.cmd` to write `Logs\RhythmValidation\rhythm_capture_handoff_last.md` |
| Rhythm blocked-state handoff | Repeated automation should explain why rhythm work cannot continue yet | Added `blockedReason` and `resumeCondition` to the next-action JSON and Markdown handoff |
| Rhythm next-action branch tests | The automation should prove every evidence state maps to the intended next action | Added `Tools\Test-RhythmNextAction.cmd` to verify NO_EVIDENCE, PARTIAL_EVIDENCE, NEEDS_TUNING, and PASS branches |
| Rhythm branch test preflight | Next-action branch regressions should fail the standard static gate | Static preflight now runs `Tools\Test-RhythmNextAction.cmd` and reports `tools.rhythmNextActionBranchTests` |
| Rhythm handoff field tests | Capture handoff fields should not silently drift from next-action branches | Expanded `Tools\Test-RhythmNextAction.cmd` to check target phase count, minimum capture count, and human capture step count |
| Rhythm blocked alternate work | Heartbeats should keep moving safely when rhythm tuning is blocked by missing captures | Added `safeAlternateAutomationActions` to next-action JSON and the Markdown handoff |
| Autonomous heartbeat status | Repeated heartbeats should share progress, validation, blocked state, and safe next steps | Added `Tools\Write-AutonomousHeartbeatStatus.cmd` to refresh rhythm handoff, safe-task JSON, and `Logs\Autonomous\autonomous_heartbeat_status_last.md` from latest preflight evidence |
| Autonomous heartbeat read-only status | Heartbeats sometimes need status without touching local evidence files | Added `Tools\Get-AutonomousHeartbeatStatus.cmd` to read the latest rhythm next-action, static preflight JSON, and WARN names/summary without writing logs |
| Autonomous heartbeat status tests | Read-only heartbeat status should not silently drop blocked-state or validation fields | Added `Tools\Test-AutonomousHeartbeatStatus.cmd`; static preflight now runs it as `tools.autonomousHeartbeatStatusTests` |
| Autonomous heartbeat writer tests | Markdown handoff should not silently drop safe-task mode, resume gate, or forbidden actions | Added `Tools\Test-AutonomousHeartbeatWriter.cmd`; static preflight now runs it as `tools.autonomousHeartbeatWriterTests` |
| Autonomous safe-task selector | Repeated heartbeats should pick safe work from evidence instead of retuning while capture-blocked | Added `Tools\Get-AutonomousSafeTask.cmd` plus `Tools\Test-AutonomousSafeTask.cmd`; the selector writes mode, block/resume, target phase, capture hotkey, human action summary, human-required, preflight timing, and forbidden-action fields into `Logs\Autonomous\autonomous_safe_task_last.json`, and static preflight runs it as `tools.autonomousSafeTaskTests` |
| Automation mode policy guard | The autonomous mode table should not silently drift out of the operations playbook | Static preflight now checks `AUTONOMOUS_OPERATIONS_PLAYBOOK_2026-06-10.md` for the mode table, all five modes, and the no-rhythm-tuning capture gate |
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
| Safe-task preflight warning visibility | Repeated automation should not lose non-blocking WARN context while choosing unattended work | Safe-task JSON and heartbeat Markdown now expose `StaticPreflightWarnCount`, `StaticPreflightHasWarnings`, `StaticPreflightWarningNames`, and `StaticPreflightWarningSummary` |
| Performance | Stinger telemetry stores primitive fields only when a stinger plays | No per-frame allocation concern |
| Risk | Some feel/audio judgment still needs a human eventually | Defer broad play validation; prioritize automated/static guards and tiny evidence captures |

## Human-Free Implementation Queue
1. Keep debug/validation displays readable and non-invasive.
2. Add or refine documents that convert design goals into testable acceptance criteria.
3. Improve generated placeholder feedback only when it does not require new licensed assets. `[done: semantic stinger clip slots and placeholder rhythm pass]`
4. Keep vendor assets ignored until explicitly approved.
5. Run static preflight after each code change and commit only scoped files.
6. Follow `AUTONOMOUS_OPERATIONS_PLAYBOOK_2026-06-10.md` when the creator is absent: choose one small task, verify with static evidence, and hand off the next action.

## Next Priority
Rhythm capture is still treated as `NO_EVIDENCE`. Do not retune rhythm feel, phase timing, pressure, payoff, or overcharge charge seconds without Play Mode / snapshot evidence.

The next best autonomous task after this 2026-08-23 slice is one of:
- Confirm Unity compile / Play Mode tap-vs-hold feel without changing numbers.
- Echo cast inside smoke: trade reveal range for lower noise. One ability interaction only.
- Keep static guardrails, spawn/checkpoint fairness, docs, or telemetry if Play Mode is blocked.

Older Release-relief guidance remains valid only after snapshots exist:
- Use `ReleaseRelief` snapshot lines to verify whether at least two relief channels stay active through the early/mid Release window.
- Prefer `Tools\Summarize-RhythmSnapshots.cmd` once snapshots exist; it should report `CalmEvidenceStatus: PASS`, `BuildEvidenceStatus: PASS`, `SpikeEvidenceStatus: PASS`, and `ReleaseEvidenceStatus: PASS` before claiming rhythm feel is proven.
- For autonomous runs, call `Tools\Write-RhythmSnapshotSummary.cmd` and read the four phase status fields from `Logs\RhythmValidation\rhythm_snapshot_summary_last.json`.
- Start with `overallEvidenceStatus`: `NO_EVIDENCE` means capture snapshots first, `PARTIAL_EVIDENCE` means capture missing phases, `NEEDS_TUNING` means tune the listed weak phase, and `PASS` means do not retune from old evidence.
- Use `Tools\Get-RhythmNextAction.cmd` after writing the summary JSON; it prints `NextAction` and `TargetPhases` for the next autonomous pass.
- Prefer `Tools\Write-RhythmNextAction.cmd` for heartbeat runs; it refreshes both `rhythm_snapshot_summary_last.json` and `rhythm_next_action_last.json`.
- Use `Tools\Write-RhythmCaptureHandoff.cmd` when notifying a person; it writes a short Markdown handoff from the next-action JSON.
- If `requiresHumanCapture=True` and `automationCanProceed=False`, stop rhythm tuning and wait for phase snapshots instead of changing feel values without evidence.
- Use `blockedReason` and `resumeCondition` to explain repeated heartbeat stalls without adding speculative tuning.
- If rhythm is capture-blocked, use `safeAlternateAutomationActions` for non-tuning work only.
- Run `Tools\RunStaticPreflight.ps1`, then use `Tools\Write-AutonomousHeartbeatStatus.cmd` to produce a single Markdown status file with progress, validation, blocked state, and recommended safe task.
- Use `Tools\Get-AutonomousHeartbeatStatus.cmd` when a heartbeat only needs to inspect the latest status without creating or changing local artifacts.
- Use `Tools\Get-AutonomousSafeTask.cmd` to choose the next unattended task from current rhythm/preflight evidence and write `Logs\Autonomous\autonomous_safe_task_last.json`.
- Read `forbiddenAutomationActions` before changing gameplay feel; if it forbids rhythm tuning, only do static guardrails, documentation, or evidence tooling.
- Read `blockedReason`, `resumeCondition`, `targetPhases`, `captureHotkey`, and `minimumCaptureCount` from safe-task JSON before asking for human capture.
- Read `StaticPreflightWarnCount`, `StaticPreflightHasWarnings`, `StaticPreflightWarningNames`, and `StaticPreflightWarningSummary` from safe-task JSON before deciding whether stale WARN evidence matters for the next claim.
- Read `StaticPreflightGeneratedAt`, `StaticPreflightDurationMilliseconds`, and `StaticPreflightDurationWarning` from safe-task JSON before deciding whether current static evidence is fresh enough for the next claim.
- Read `humanActionSummary` when a short one-line request is enough for the creator.
- Route unattended work from `automationMode`: `SAFE_ALTERNATE_ONLY`, `FIX_FAILURES_ONLY`, `RHYTHM_TUNING_ALLOWED`, or `REFRESH_STATUS`.
- Follow `AUTONOMOUS_OPERATIONS_PLAYBOOK_2026-06-10.md` for the full `automationMode` table, including `RHYTHM_AUTOMATION_ALLOWED`.
- Run `Tools\Test-AutonomousHeartbeatStatus.cmd` after changing heartbeat status output; static preflight also runs it as `tools.autonomousHeartbeatStatusTests`.
- Run `Tools\Test-AutonomousHeartbeatWriter.cmd` after changing the Markdown heartbeat handoff; static preflight also runs it as `tools.autonomousHeartbeatWriterTests`.
- Run `Tools\Test-AutonomousSafeTask.cmd` after changing safe-task selection; static preflight also runs it as `tools.autonomousSafeTaskTests`.
- Run `Tools\Test-RhythmNextAction.cmd` after changing next-action logic; static preflight also runs it as `tools.rhythmNextActionBranchTests`.
- Read `humanCaptureSteps` from `Tools\Get-RhythmNextAction.cmd -OutputJsonPath ...` when handing the task to a person; it is the shortest acceptable capture path.
- If snapshots still show weak relief, tune intensity/fade shape next rather than adding new assets.
- Keep Spike fairness tuning as the second priority: use warning -> threat -> response telemetry to adjust tell lead time or chase timing only when snapshots show an unfair spike.

The continuing autonomous validation policy is:
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
