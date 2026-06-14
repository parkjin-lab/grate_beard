# Autonomous Operations Playbook - 2026-06-10

## Purpose
Keep development moving when the creator is busy or absent, without broad manual play validation or risky repository changes.

## Heartbeat Procedure
1. Check `git status --short` first. Never revert or stage unrelated changes.
2. Pick one small, human-free task that improves fun, reliability, validation, or documentation.
3. Prefer static evidence: `git diff --check` and `Tools/RunStaticPreflight.ps1`.
4. For rhythm work, run `Tools\Write-RhythmSnapshotSummary.cmd` and then `Tools\Get-RhythmNextAction.cmd` before choosing capture, tuning, or variation work.
5. Treat `FAIL` as blocking. Treat stale log `WARN` as a refresh signal unless claiming release readiness.
6. Commit only explicit files. Do not use `git add .`.
7. Push successful commits to `origin/main`.
8. End with the current evidence, remaining risk, and the next 1-3 autonomous actions.

## Current Autonomous Priority
1. Spike fairness instrumentation: prove warning, threat, and response windows are connected.
2. Release relief contract: prove at least two non-text relief channels remain active long enough to feel like an exhale.
3. Build temptation: prove Build creates a risky route choice, not only rising pressure.
4. Rhythm variation: prevent stage set-pieces from feeling identical every cycle.
5. Korean-facing wording and encoding guardrails: keep player feedback readable and not debug-like.

## Human-Free Work List
- Add static preflight hooks when a new rhythm, spawn, UI, audio, or validation contract is introduced.
- Convert abstract design goals into machine-readable counters or snapshot lines.
- Keep resource requirement docs updated when code creates a new art/audio need.
- Separate player-facing Korean copy from developer/debug English.
- Add lightweight scripts that read existing evidence without mutating logs.
- Use `Tools\Get-RhythmNextAction.cmd` to convert rhythm summary JSON into the next autonomous action.
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
