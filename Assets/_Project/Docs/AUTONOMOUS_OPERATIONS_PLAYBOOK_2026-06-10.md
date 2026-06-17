# Autonomous Operations Playbook - 2026-06-10

## Purpose
Keep development moving when the creator is busy or absent, without broad manual play validation or risky repository changes.

## Heartbeat Procedure
1. Check `git status --short` first. Never revert or stage unrelated changes.
2. Pick one small, human-free task that improves fun, reliability, validation, or documentation; use `Tools\Get-AutonomousSafeTask.cmd` when the next safe task is ambiguous.
3. Prefer static evidence: `git diff --check` and `Tools/RunStaticPreflight.ps1`; for repeated heartbeats, use `Tools\Get-AutonomousHeartbeatStatus.cmd` for read-only inspection, or run `Tools\RunStaticPreflight.ps1` and then `Tools\Write-AutonomousHeartbeatStatus.cmd` when refreshing the handoff artifact.
4. For rhythm work, run `Tools\Write-RhythmNextAction.cmd` before choosing capture, tuning, or variation work.
5. If rhythm next action reports `requiresHumanCapture=True` and `automationCanProceed=False`, do not retune rhythm feel from stale or missing evidence; report its `blockedReason` and `resumeCondition`.
6. If a person needs to act, run `Tools\Write-RhythmCaptureHandoff.cmd` and reference its Markdown handoff; use only `safeAlternateAutomationActions` for unattended work.
7. Treat `FAIL` as blocking. Treat stale log `WARN` as a refresh signal unless claiming release readiness.
8. Commit only explicit files. Do not use `git add .`.
9. Push successful commits to `origin/main`.
10. End with the current evidence, remaining risk, and the next 1-3 autonomous actions.

## Current Autonomous Priority
1. Spike fairness instrumentation: prove warning, threat, and response windows are connected.
2. Release relief contract: prove at least two non-text relief channels remain active long enough to feel like an exhale.
3. Build temptation: prove Build creates a risky route choice, not only rising pressure.
4. Rhythm variation: prevent stage set-pieces from feeling identical every cycle.
5. Korean-facing wording and encoding guardrails: keep player feedback readable and not debug-like.

## Automation Mode Policy
| automationMode | Allowed work | Forbidden work | Exit condition |
| --- | --- | --- | --- |
| `SAFE_ALTERNATE_ONLY` | Static guardrails, documentation, evidence tooling, status/handoff improvements | Rhythm feel tuning, broad Play Mode validation, claiming Spike/Release feel is proven | Capture one Calm, Build, Spike, and Release rhythm snapshot, then rerun `Tools\Write-RhythmNextAction.cmd` |
| `FIX_FAILURES_ONLY` | Fix failing static preflight checks and malformed evidence outputs | Feature work, tuning, release-readiness claims | `Tools\RunStaticPreflight.ps1` returns `fail=0` |
| `RHYTHM_TUNING_ALLOWED` | Tune only the weak phase named by rhythm evidence; keep changes small and re-summarize snapshots | Unrelated refactors, changing phases without evidence, claiming untested phases are fixed | Snapshot summary no longer reports the tuned phase as weak |
| `RHYTHM_AUTOMATION_ALLOWED` | Follow the next rhythm automation action from evidence, usually variation or non-tuning progression | Retuning feel values unless the action is `TUNE_WEAK_PHASES` | Next-action JSON advances or returns to capture/tuning gate |
| `REFRESH_STATUS` | Refresh static preflight, rhythm summary, safe-task JSON, and heartbeat status | Gameplay changes based on missing status evidence | Required JSON summaries exist and safe-task mode is no longer `REFRESH_STATUS` |

## Human-Free Work List
- Add static preflight hooks when a new rhythm, spawn, UI, audio, or validation contract is introduced.
- Convert abstract design goals into machine-readable counters or snapshot lines.
- Keep resource requirement docs updated when code creates a new art/audio need.
- Separate player-facing Korean copy from developer/debug English.
- Add lightweight scripts that read existing evidence without mutating logs.
- Use `Tools\Write-RhythmNextAction.cmd` to refresh rhythm summary JSON and convert it into the next autonomous action.
- Use `Tools\Get-AutonomousHeartbeatStatus.cmd` to inspect current rhythm/preflight status without mutating logs.
- Use `Tools\Get-AutonomousSafeTask.cmd` to convert current rhythm/preflight evidence into the next safe unattended task and refresh `Logs\Autonomous\autonomous_safe_task_last.json`.
- Obey `forbiddenAutomationActions` from the safe-task JSON before touching gameplay feel, especially when `humanRequired=True`.
- Route autonomous behavior from `automationMode`; `SAFE_ALTERNATE_ONLY` means no rhythm feel changes until capture evidence exists.
- Use `Tools\Write-AutonomousHeartbeatStatus.cmd` after static preflight when a heartbeat needs one concise progress/validation/blocked-state/safe-task artifact.
- Use `Tools\Test-AutonomousHeartbeatStatus.cmd` before committing changes to heartbeat status output; `Tools\RunStaticPreflight.ps1` also runs it.
- Use `Tools\Test-AutonomousHeartbeatWriter.cmd` before committing changes to the Markdown heartbeat handoff; `Tools\RunStaticPreflight.ps1` also runs it.
- Use `Tools\Test-AutonomousSafeTask.cmd` before committing changes to safe-task selection; `Tools\RunStaticPreflight.ps1` also runs it.
- Use `Tools\Test-RhythmNextAction.cmd` before committing changes to rhythm next-action branching; `Tools\RunStaticPreflight.ps1` also runs it.
- Keep stale validation evidence visible but do not block non-release commits on it.

## Validation Rules
- Minimum for code commits: `git diff --check` and static preflight `fail=0`.
- Preferred for Unity-touching gameplay commits: Unity compile or targeted editor regression when available.
- Do not commit generated logs, rhythm snapshots, soak traces, or vendor imports.
- If Unity is unavailable, keep changes small and backed by static hooks.

## Next Agent Handoff
- Next implementation target: Release relief tuning from snapshot evidence in `ThreatReadabilityDirector`, `DreadScreenOverlayRuntime`, and `DebugOverlay`.
- Acceptance target: during Release, `ReleaseRelief` should usually show at least two active non-text channels; tune durations/intensity before adding new asset needs.
- Secondary target: tune Spike warning lead time or chase timing only if the Spike fairness telemetry reports an unfair warning -> threat chain.
