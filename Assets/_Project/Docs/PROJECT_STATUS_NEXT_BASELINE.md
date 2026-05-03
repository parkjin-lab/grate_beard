# LostBreadcrumbs Project Status (Next-Step Baseline)

Updated: 2026-04-16

## 1) Current Implemented Loop
- Sequential stage generation + stage advance via exit portal
- Breadcrumb objective, stamina pickup, safe haven sustain
- Fog of war + flashlight cone reveal
- Player abilities: Echo Pulse / Decoy / Smoke
- Enemy spawn + profile-based behavior + learning telemetry
- Event bus + HUD feed + feedback + audio ducking + dummy loops
- Save/load/new-run checkpoint flow
- Run loadout system (runtime selectable, lock/unlock, debug visible)
- Spatially expanded map generation (multi-cell room/hideout/corridor footprints for better testability)
- Runtime map tuning presets (Compact/Standard/Expansive) with hotkey cycling and instant regeneration
- Cell-kind density pass: breadcrumb/safe-haven/enemy spawn now use Room/Fork/Hideout/Corridor weighted selection
- Map visual clarity pass: boundary walls + camera auto-fit to generated map bounds
- Audio tension pass: enemy hearing now attenuates through walls with per-noise-type transmission
- Room archetype pass: map now generates cover occluders by cell kind (Room/Fork/Hideout/Risk) for LOS and routing pressure
- Room archetype interaction pass: proximity hook dummies now spawn by cell kind and emit contextual noise to create route tension
- Camera + fog readability pass: camera follow bounds clamp + adaptive fog reveal/resolution scaling for large maps
- Stage pressure balance pass: stage+telemetry driven enemy spawn pressure and ability cooldown economy
- Regression checklist automation pass: runtime hotkey checklist now verifies map generation, pressure wiring, and death-reset flow
- Hook runtime tuning pass: archetype hooks now scale by stage + map preset intensity with live multipliers
- Hook readability pass: pre-emit warning telegraph + hierarchy probe dummy (`HookTensionProbe_Stage_*`) for inspector validation
- Hook cue authoring pass: variant-specific sigil cues now replace plain debug orbs while preserving telegraph timing
- Death recap toast pass: death now surfaces `cause + pressure + missed option` as a 3-second runtime toast
- Event stinger pass: runtime context stingers now trigger on `Exit unlocked` and `chase started` events
- Late-stage pacing pass: stage 5+ now adds breadcrumb pressure while reducing safe-haven density/radius and stamina economy
- Camera/fog final art pass: runtime camera background grading + fog tint/alpha styling + threat spike camera impulse
- Sigil/stinger readability playtest pass: stage 1/3/5 now scales hook telegraph lead/pulse/visual weight and stinger stage intensity
- Threat readability pass: camera zoom/look-ahead + fog reveal/refog + enemy perception are now pressure-driven by stage/nearby threat
- Threat readability runtime probe: `ThreatReadabilityDirector` tracks N/S/F pressure and applies runtime multipliers to camera/fog/enemy senses
- Save/load regression pass: checkpoint now captures/restores stage pressure economy + threat readability state snapshots for deterministic resume flow
- Agent event readability pass: enemies now raise lock-on/chase/disengage runtime events near player for stronger situational feedback
- Probe safety pass: Hook tension probe dummy generation is now editor/debug-gated unless explicitly allowed
- Chase transition readability polish pass: lock-on now syncs marker/body flash, chase disengage shows `?` cue, and re-acquire hysteresis prevents flicker re-chase
- Stage tier set-piece pass: stage 3/5/7 now build signature beacon beats with reinforcement spawns and runtime hierarchy dummies (`Runtime/SetPieces`)
- Preset x stage matrix pass: `RegressionChecklistRunner` now validates Compact/Standard/Expansive x stage 1/3/5 (pressure/readability curves + set-piece coverage) with debug overlay summaries
- Chase readability regression pass: checklist now verifies chase-transition/disengage/blink tuning trends across preset x low/high stage pressure and exposes summary in debug overlay
- Set-piece dynamic tuning pass: beacon/reinforcement/pulse cadence now scales by stage pressure + map preset with safety caps and live overlay telemetry
- Matrix threshold tuning pass: preset-stage matrix now tolerates minor curve dips, sync-waits set-piece build, and supports baseline lock/envelope drift checks for false-positive pruning
- UX/game-feel polish pass: priority cue toast (lock-on/chase/exit/death), death recap hold-fade-tip pacing, and HUD alert dedupe/canonical labels for faster combat readability
- Final playtest balancing pass: stage-envelope caps now gate set-piece intensity/tension and threat-readability chase aggression to reduce early over-spike while preserving late-stage pressure
- Baseline calibration pass: matrix baseline policy controls (auto-lock/refresh/require/frozen), manual lock-from-last-run action, and playbook documentation landed
- UX micro-iteration pass: priority/death cue durations, fade velocity, color/alpha, and font emphasis now scale with stage intensity for clearer late-stage urgency
- Regression matrix threshold final lock pass: one-click final lock policy (menu/context), strict policy snapshot, and debug overlay readiness summary landed
- Release-candidate soak harness pass: iterative save/load/new-run/death-reset regression automation + disk-write suppression + debug/menu launch flow landed
- Release checklist freeze pass: release gate summary, freeze-defaults one-click action, and editor/overlay gate logging landed
- Soak triage visibility pass: regression panel now toggles `Checklist/Soak` entry source and shows compact soak failure digest + one-click failure log
- Soak triage action-plan pass: soak failures now auto-summarize by iteration (`I#:count`) and emit prioritized fix actions in overlay/console/menu
- Soak detailed-report pass: one-click full soak report output (`summary + digest + actions + full entries`) added to overlay/menu for shareable triage logs

## 2) Foundation Added For Next Steps
- Loadout tuning moved to catalog-ready data model:
  - `RunLoadoutCatalog` ScriptableObject
  - `RunLoadoutTuning` data struct
- Setup pipeline now auto-creates default loadout catalog and assigns it to `RunLoadoutDirector`
- Debug overlay now shows map bounds + map kind distribution + map preset source + set-piece tier/beacon/reinforcement state + regression matrix/chase summaries
- SaveManager now persists selected/unlocked loadout state and restores it on startup/checkpoint load

## 3) Remaining High-Impact Next Tasks
1. Release-candidate soak execution pass (run harness, collect failure logs, tune/fix regressions)

## 4) Entry Criteria For Next Step
- Confirm compile is clean in Unity Console
- Verify each map preset (`Compact/Standard/Expansive`) across stage 1/3/5
- Check that death/new-run reset behavior still holds with expanded maps

## 5) Quick Verify Commands (Editor)
- `LostBreadcrumbs/Setup/Build Full Playground`
- `LostBreadcrumbs/Gameplay/Map Preset/Standard (Recommended)`
- Play Mode keys: `F2`, `F3`, `F5`, `F6`, `F7`, `F9`, `F10`, `F11`, `F12`, `BackQuote(\`)`

























