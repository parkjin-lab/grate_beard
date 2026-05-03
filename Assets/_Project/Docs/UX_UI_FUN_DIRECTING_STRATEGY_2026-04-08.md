# LostBreadcrumbs UX + UI + Core Fun Strategy

Updated: 2026-04-09

## Sub-Agent Track A - UX Procedure Audit (Findings)
| Problem | Player Impact | Action |
| --- | --- | --- |
| Blinking hook orbs had no in-game explanation | Confusion: players cannot map visual cue to risk/noise | Added runtime flow guide line: `Map Cues: Hook Sigils (spin/blink = pre-noise warning)` |
| Objective was numeric-only (`Breadcrumb X/Y`) | Weak sense of current run phase | Added explicit flow step label (`Explore`, `Collect`, `Escape`) |
| Pressure/readability values were mostly debug-facing | Hard to learn danger rhythm during normal play | Added compact danger line (`StageP/ThreatP/TotalP`) |
| Ability cooldown awareness relied on HUD scanning | Missed tactical windows for Echo/Decoy/Smoke | Added dedicated cooldown line in guide panel |
| Control onboarding was implicit | New/returning players lose first 30s to key hunting | Added always-visible controls hint line |

## Sub-Agent Track B - Dummy UI Attachment
Implemented `GameplayFlowGuideRuntime`:
- Runtime auto-build panel at top-right.
- Small, non-interactive overlay (separate canvas, sort order below main HUD).
- Safe fallback text when systems are missing.
- Data links: `StageLoopDirector`, `MapSystem`, ability cooldowns, `StagePressureDirector`, `ThreatReadabilityDirector`.
- Auto-attached in setup pipeline via `LostBreadcrumbsProjectSetup`.

## Sub-Agent Track C - Core Fun + Direction Reinforcement
### Core fun pillars
1. Risk reading: hear/see danger, then reroute.
2. Micro-decision economy: Echo vs Decoy vs Smoke timing.
3. Extraction tension: unlock exit, survive final route.

### High-leverage design moves
1. Pressure beat windows: every stage should contain at least one low-pressure breath and one high-pressure spike.
2. Cue consistency: every high-risk mechanic should have one visual and one audio pre-cue.
3. Rewarded mastery: faster exit unlocks or no-hit room clears should produce small positive feedback.
4. Failure clarity: death recap should show `cause + pressure + missed option` in one line.

## Direction Upgrade Plan (Execution)
### Sprint 1 (Now)
1. Keep new flow guide panel enabled by default in prototype scene.
2. Tune phase thresholds and wording with 3 quick playtests.
3. Validate no overlap with existing HUD on 16:9 and 21:9.

### Sprint 2
1. Replace debug hook visuals with style-matched cue assets (same meaning, better readability).
2. Add short audio stinger for `exit unlocked` and `chase spike`.
3. Add death recap toast (3 seconds) before respawn.

### Sprint 3
1. Add late-stage pacing rules (safe-haven scarcity + route pressure escalation).
2. Add one signature set-piece per stage tier (stage 3/5/7).
3. Capture retention telemetry: first death time, average exit time, ability usage cadence.

## Success Metrics
1. First-run confusion reports about controls/cues: down.
2. Ability usage diversity (Echo/Decoy/Smoke each used): up.
3. Stage clear rate from 1 -> 3 without churn: up.
4. "Unclear death" feedback: down.
## 2026-04-09 Applied Improvements (Agent + Next-Step)
1. Agent readability feedback: enemy now emits runtime events for `lock-on warning`, `chase started`, and `chase disengaged`.
2. Probe safety: `HookTensionProbe` dummy now follows editor/debug gating by default to avoid shipping debug hierarchy noise.
3. Next-step focus update: prioritize `hook cue art replacement` and `death recap toast` as the shortest path to perceived quality.
4. Hook cue art replacement landed: `RoomArchetypeHookDummy` now builds variant-specific sigil sprites (chain/glass/vent/cloth/alarm/metal) with warning telegraph animation.
5. Death recap toast landed: `EventFeedbackRuntime` now shows `cause + pressure + missed option` for 3 seconds on death.
6. Event stinger pass landed: `AudioManager` now plays dedicated stingers for `Exit unlocked` and enemy `chase started` spike moments.
7. Late-stage pacing pass landed: stage 5+ now ramps breadcrumb pressure while shrinking safe-haven and stamina economy windows.
8. Camera/fog final art pass landed: pressure now drives camera background grade, fog tint/alpha styling, and threat spike camera impulse.
9. Sigil/stinger readability playtest pass landed: hook telegraph lead/pulse/scale and stinger stage intensity are now stage-scaled with runtime debug readouts.
10. Chase transition readability polish landed: enemy lock-on now synchronizes marker/body flash, chase disengage shows `?` cue, and re-acquire delay reduces jittery re-chase.
11. Stage tier set-piece implementation landed: `StageSetPieceDirector` now builds stage 3/5/7 signature beats (beacons + reinforcements), auto-wired in setup with runtime `SetPieces` hierarchy + debug overlay visibility.
12. Preset x stage matrix automation landed: `RegressionChecklistRunner` now runs Compact/Standard/Expansive x 1/3/5 validation (pressure/readability curve + set-piece coverage) and surfaces summary in debug overlay/panel.
13. Chase readability regression automation landed: checklist now verifies chase transition/disengage/blink trend sanity across preset x low/high pressure and reports pass/fail in overlay/panel.
14. Set-piece dynamic tuning landed: `StageSetPieceDirector` now applies stage-pressure + map-preset intensity scaling to beacon/reinforcement/pulse cadence with runtime safety caps and overlay telemetry.

## Next-Step Execution Board (Revised)
1. Tune matrix/chase thresholds (reduce false positives, lock baseline).
2. Final UX/game-feel pass (readability event wording + recap pacing).
3. Final playtest balancing (set-piece + chase readability envelope).















