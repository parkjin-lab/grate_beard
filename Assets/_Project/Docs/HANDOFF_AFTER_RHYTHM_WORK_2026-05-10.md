# Handoff After Rhythm Work
Updated: 2026-05-10

## Start Here
Before continuing, run:
- `git status --short`
- `git diff --check`
- Unity Editor compile

Do not clean or revert unrelated changes. The workspace contains in-flight gameplay edits plus large untracked asset/recovery folders.

## What Just Changed
- Added `GameplayRhythmDirector` with Calm, Build, Spike, Release phases.
- Connected rhythm to pressure shaping, audio loop pitch/dread tension, debug overlay, runtime events, regression checks, and `SampleScene`.
- Added `RuntimeEventSemantic.RhythmShift` and UI/priority cue handling.
- Added rhythm design review and this updated documentation set.
- Earlier in-flight work also includes save/load transient cleanup, objective target correction, and soak checks for transient reset.

## Immediate Next Step
Open Unity and validate the runtime. The safest order:
1. Confirm no Unity Console compile errors.
2. Enter Play Mode in `SampleScene`.
3. Confirm DebugOverlay shows rhythm telemetry.
4. Run F11 regression.
5. Run release soak with report file.
6. Play stage 1 through stage 3 and judge phase feel.

## Watch Points
- `GameplayRhythmDirector` was added as a new script, so Unity reimport is expected.
- `SampleScene` was edited directly to include the new manager; inspect if Unity rewrites serialized field ordering.
- Rhythm pressure modulation is disabled during regression runs to preserve deterministic pressure checks.
- Spike adds pressure and camera impulse; check it does not stack unfairly with set-pieces or enemy density.
- Release currently exists more as pressure relief than player-facing relief. That is the next design gap.

## Best Next Feature Work After Validation
1. Make Release legible through objective whisper, room tone, stamina relief, or enemy recovery behavior.
2. Make Build surface risk cache and breadcrumb momentum more strongly.
3. Tie exit unlock to a short Build/Spike/Release escape beat.
4. Give breadcrumbs phase-specific rewards.
5. Move set-piece timing toward Build-late or Spike entry.
