# Project Status Update
Updated: 2026-05-10

## Current Read
- The project now has a macro rhythm layer through `GameplayRhythmDirector`: Calm -> Build -> Spike -> Release.
- `SampleScene` includes the rhythm director and wires it to `StagePressureDirector`, `ThreatReadabilityDirector`, `AudioDummyLoopRuntime`, and runtime debug visibility.
- Stage pressure is no longer purely continuous. Runtime pressure can now breathe by phase while regression runs keep deterministic checks.
- Audio fallback loops now read rhythm tempo/intensity, so pitch and dread drone tension can move with the same pacing.
- Debug and regression tooling now expose rhythm state through overlay telemetry and checklist checks: `Rhythm.Enabled`, `Rhythm.PressureShape`, `Rhythm.Telemetry`.

## Recent Stability Work In Flight
- Save/load and new-run paths now clear unsaved transient effects such as quiet breath, temporary concealment, pulse/decoy/smoke runtime effects, and flashlight dread tuning.
- Objective targeting was tightened so locked exits do not steal guidance from distant active breadcrumbs.
- Regression soak checks were expanded to verify transient cleanup after load and new-run flows.

## Validation State
- C# compile was run through the local Roslyn/Unity response file with the new rhythm script explicitly included. It passed.
- `git diff --check` passed.
- Remaining warnings are existing Unity analyzer/obsolete API warnings, not new rhythm compile blockers.
- Unity Editor Play Mode, F11 regression, release soak, and hands-on playtest have not yet been run after the rhythm update.

## Workspace Notes
- Branch is `main`.
- Important modified tracked files include `SampleScene.unity`, `LostBreadcrumbsProjectSetup.cs`, `RuntimeEventBus.cs`, `StagePressureDirector.cs`, `AudioDummyLoopRuntime.cs`, `RegressionChecklistRunner.cs`, `SaveManager.cs`, `ThreatReadabilityDirector.cs`, `StageLoopDirector.cs`, HUD/feedback/debug UI files.
- New rhythm/design docs and script files are untracked until staged.
- Large untracked asset/recovery folders remain: `Assets/Feel`, `Assets/Layer Lab`, `Assets/ThirdParty`, `Assets/_Recovery`. They were not classified or cleaned in this pass.

## Project Judgment
The game now has the right architectural hook for rhythm, but the design is only halfway converted. Pressure can pulse, but the next work must make each phase change what the player wants to do: read, risk, survive, recover.
