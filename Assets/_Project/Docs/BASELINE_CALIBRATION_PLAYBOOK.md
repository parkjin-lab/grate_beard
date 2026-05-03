# Baseline Calibration Playbook

Updated: 2026-04-16

## Goal
- Keep `RegressionChecklistRunner` matrix checks stable across balancing iterations.
- Prevent accidental threshold drift after a good playtest baseline is captured.

## Baseline Modes
- `autoLock=Y`: If no baseline exists and matrix run passes, baseline is captured automatically.
- `autoRefresh=Y`: If baseline exists and envelope check passes, baseline is refreshed to current run.
- `require=Y`: Matrix run fails when baseline is missing.
- `frozen=Y`: Baseline cannot be auto-locked or auto-refreshed.

## Recommended Workflow
1. Choose target config (`Standard` preset, stage 1/3/5 sanity run).
2. Run checklist (`F11`) until matrix is stable.
3. Capture baseline:
- Either rely on auto-lock, or use context menu `Lock Matrix Baseline From Last Matrix Run`.
4. Freeze baseline:
- Use context menu `Toggle Matrix Baseline Frozen` so future runs cannot overwrite.
5. During tuning:
- Keep `autoRefresh=N`, `require=Y`, `frozen=Y` for strict gate.
6. Intentional recalibration:
- `Toggle Matrix Baseline Frozen` (unfreeze), run checklist on new target tuning, re-lock, freeze again.

## Final Lock Shortcut (Release Prep)
- Use `LostBreadcrumbs/Gameplay/Regression/Apply Matrix Final Lock Policy` once calibration is accepted.
- This applies strict gate defaults in one action:
- `autoLock=N`, `autoRefresh=N`, `require=Y`, `frozen=Y`, baseline-envelope affects pass.
- If baseline is missing but the last matrix run passed, it auto-captures from that run before freezing.
- Validate in overlay: `Regression Matrix Final Lock` should show `ready=Y`.

## Release Soak Trigger
- Use `LostBreadcrumbs/Gameplay/Regression/Run Release Candidate Soak Pass` in Play Mode.
- Hotkey: `F2` (same runner object).
- Soak run checks repeated `Save -> mutate -> Load -> DeathReset -> NewRun -> MatrixGate`.
- Disk write suppression is applied by default during soak, so persistent save JSON is not overwritten.
- Validate in overlay: `Release Soak`, `Release Soak Detail`, `Soak Failures`.
- In regression panel, toggle entry source with `BackQuote(\`)` to inspect `Soak` entries directly.
- For console triage, use panel button `Soak Fail Log` (or context menu `Log Release Soak Failures`).
- Action guidance is now auto-generated as `Soak Actions` with `Soak Iterations` (`I#:failCount`) to prioritize fix order.
- Use `LostBreadcrumbs/Gameplay/Regression/Log Release Soak Action Plan` (or panel `Soak Action Log`) for one-line next actions.
- For full handoff logs, use `Log Release Soak Detailed Report` (or panel `Soak Report Log`) to print summary + all entries.
- For shareable file artifacts, use `Write Release Soak Detailed Report File` (or panel `Soak Report File`) to save a timestamped log under `Logs/ReleaseSoak/`.
- One-click path: `Run Release Soak + Write Report File (Auto)` enters Play Mode if needed, runs soak, then writes the report file automatically.

## Release Checklist Freeze
- Use `LostBreadcrumbs/Gameplay/Regression/Apply Release Checklist Freeze Defaults` before final candidate verification.
- This enforces strict release defaults for matrix/chase/soak gates and marks freeze state on runner.
- Gate status is exposed via `Release Checklist Gate` in DebugOverlay.
- Use `LostBreadcrumbs/Gameplay/Regression/Log Release Checklist Gate` to print the current gate verdict to console.
- Gate `ready=Y` requires freeze applied (or freeze requirement disabled), final-lock ready, checklist pass, matrix pass, chase pass, and soak pass.

## Failure Triage
- `Matrix.BaselineEnvelope FAIL`:
- Tuning changed beyond allowed drift. Check set-piece intensity/readability envelope first.
- `Matrix.BaselineRequired FAIL`:
- Baseline missing while `require=Y`. Capture baseline before continuing.
- Frequent false fail with valid gameplay:
- Increase `matrixBaselineDriftTolerance` slightly and rerun matrix.

## Quick Debug Overlay Checks
- `Regression Matrix`
- `Regression Matrix Detail`
- `Regression Matrix Baseline`
- `Regression Matrix Baseline Policy`
- `Release Soak`
- `Soak Failures`
- `Soak Iterations`
- `Soak Actions`
