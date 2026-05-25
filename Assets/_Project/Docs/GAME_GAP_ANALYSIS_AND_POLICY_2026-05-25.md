# Game Gap Analysis And Improvement Policy - 2026-05-25

## Purpose
This document consolidates a three-agent review of the current game design, horror readability, and production readiness. The goal is to define what the game is missing now and set concrete policies for how future updates should improve the core loop without losing control of difficulty, readability, or release quality.

Current branch context at review time:
- Branch: `main`
- Remote: `origin/main`
- Latest pushed commit observed before this document: `ef33135`
- Untracked vendor/import scope still present: `Assets/Feel`, `Assets/Layer Lab`, `Assets/ThirdParty.meta`

## Agent Roles
- Pasteur: Core loop and rhythm design reviewer. Focused on `GameplayRhythmDirector`, `StagePressureDirector`, `StageLoopDirector`, breadcrumbs, risk caches, and exit flow.
- Dalton: Horror, readability, audio, visual feedback, and player-facing language reviewer. Focused on `ThreatReadabilityDirector`, `AudioManager`, `AudioDummyLoopRuntime`, fog, enemy tells, HUD, and feedback surfaces.
- Bernoulli: Validation, release, source-control, and operational policy reviewer. Focused on verification docs, release soak evidence, static preflight, untracked assets, and CI gaps.

## Main Finding
The game now has useful pressure and rhythm systems, but the rhythm is still too often expressed as numbers, multipliers, and debug feedback. The next design step is to make each phase change the player's actual decision.

Target phase contract:
- Calm: read the room, understand route risk, and plan.
- Build: tempt the player with a reward that is worth considering.
- Spike: test the consequence of earlier choices with fair warning.
- Release: give a felt, playable exhale and prepare the next route.

If a feature does not change how the player reads, risks, escapes, recovers, or plans, it should not be treated as a core rhythm feature yet.

## Missing Areas

### 1. Rhythm exists, but not enough as player choice
`GameplayRhythmDirector` defines Calm, Build, Spike, and Release, but most runtime effects are still pressure shaping, duration, camera impulse, and events. Pickups, cache rewards, exit decisions, and route information need stronger phase behavior.

Policy:
- Every major loop object must answer: what changes in Calm, Build, Spike, and Release?
- Phase changes should create different player questions, not only different values.
- Global pressure tuning is not enough unless it also changes a concrete decision.

Implementation direction:
- Add phase-aware reward rules to `Assets/_Project/Scripts/Map/StageLoopDirector.cs`.
- Expose small, readable helper data from `Assets/_Project/Scripts/Managers/GameplayRhythmDirector.cs`.
- Start with breadcrumbs, risk caches, exit unlock, and release relief because they are already central to the loop.

### 2. Release is not reliably felt as relief
Release lowers pressure, but the player needs to feel it without reading debug UI. Current relief is too dependent on chase disengage or short-lived reward effects.

Policy:
- Release must express through at least two non-text channels for 2.5 to 4 seconds.
- Valid release channels: softer dread layer, slightly wider route readability, objective whisper, stamina breath, enemy search hesitation, reduced breadcrumb corruption, or calmer room tone.
- Release must never be only a number reduction or toast message.

Implementation direction:
- Extend the existing relief path around `ThreatReadabilityDirector` and rhythm phase entry.
- On Release entry, provide one sensory relief and one gameplay relief.
- Add manual feel-test criteria: "Can the player tell they survived the crest within 2 seconds?"

### 3. Build temptation is too static
Risk caches and breadcrumb momentum work, but they do not yet create enough "take it now or stay safe" tension. The reward/cost model should be more phase-aware.

Policy:
- Build should be the main temptation phase.
- Risk caches should scale both reward and danger by phase.
- Breadcrumb streaks should buy route clarity or future relief, not only stamina.

Implementation direction:
- In `RiskCachePickup`, support phase-aware reward, noise, and aftershock values.
- In `StageLoopDirector`, let Build caches become more attractive while Spike caches become louder but potentially clutch.
- Let breadcrumb streaks improve the next exit/cache signal or strengthen the next Release.

### 4. Spike fairness needs a global cue budget
Threat systems can stack camera impulse, fog tuning, enemy perception, dread beats, phantom cues, close stalker cues, pressure waves, stingers, and set-piece pressure. Individually these are useful, but together they can feel unfair.

Policy:
- Every Spike must have a readable tell before the punishment window.
- Spike should amplify consequences of previous player choices instead of spawning unrelated new problems.
- Camera shake, screen flash, fog loss, enemy aggression, and major stingers should not all peak in the same beat.
- Use a cue budget: at most one major stinger within 3 seconds, with minor cues ducked under it.

Implementation direction:
- Centralize gating in `ThreatReadabilityDirector`.
- Add a "spike tell before spike hit" manual test.
- Treat cue overload as a gameplay bug, not just presentation tuning.

### 5. Audio is still scaffolding more than horror language
`AudioManager` has strong starts for exit unlock and chase, but many events still fall back to generated tones or generic cues. `AudioDummyLoopRuntime` and runtime-synthesized cues are valuable for prototyping, but not enough for final identity.

Policy:
- Each priority semantic needs a distinct audio role.
- Required semantic roles: `LockOnWarning`, `ChaseStarted`, `EscapeRelief`, `QuietBreathBroken`, `EchoReturn`, `RhythmShift`, `SetPieceShift`, `ExitUnlocked`, `Death`.
- Generated audio is allowed as fallback only; production direction should move toward authored, recognizable audio motifs.

Implementation direction:
- Expand stinger mapping in `Assets/_Project/Scripts/Managers/AudioManager.cs`.
- Keep a hard stinger budget so horror rhythm has silence, anticipation, and impact.
- Replace dummy loops gradually, starting with chase, release, and echo return.

### 6. Player-facing UI still leaks debug language
HUD and guide surfaces mix pressure numbers, English debug labels, Korean status text, and gameplay terms. That is useful during development but weakens immersion.

Policy:
- Player-facing UI should use action language.
- Numeric pressure, phase names, and raw telemetry belong in `DebugOverlay`.
- Choose Korean-first player language unless a specific English alarm style is intentionally defined.
- Maintain a small glossary so the same event is not named differently in HUD, toast, and audio docs.

Implementation direction:
- Split debug and player surfaces in `GameplayFlowGuideRuntime`, `GameplayHudRuntime`, and `EventFeedbackRuntime`.
- Keep debug overlays rich, but make normal play screens more diegetic and concise.

### 7. Visual horror identity is readable but still abstract
Movement echoes, dread overlay, pulses, and lines are functional, but the game still lacks a specific visual motif set that says "this is our horror language."

Policy:
- Keep readability first, then replace dummy presentation with a small motif set.
- Motifs should be diegetic where possible: enemy-specific tells, breadcrumb corruption, room-tone fog behavior, restrained screen effects.
- Accessibility matters: fog/dread effects must remain readable and tunable.

Implementation direction:
- Define one motif per semantic group: enemy tell, route echo, corruption, relief, and death.
- Avoid adding many decorative effects until the motif set is stable.

### 8. Verification is not yet evidence-based enough
Current docs still mark Unity compile, `SampleScene` Play Mode, F11 regression, release soak, and Stage 1-3 manual feel test as unverified for the latest state. Historical logs are useful, but stale logs cannot prove the current build.

Policy:
- No iteration is green unless a dated artifact includes:
  - Unity Editor console compile status
  - Play Mode smoke result
  - F11 regression result
  - Release soak report path
  - Stage 1-3 manual feel notes
  - Branch, commit, Unity version, and dirty/untracked summary
- A release candidate requires `Release Checklist Gate ready=Y`; partial soak pass is diagnostic only.

Implementation direction:
- Run fresh verification after the next gameplay implementation.
- Promote `Tools/RunStaticPreflight.ps1` into a required local/CI gate.
- Add CI around static preflight and Unity batchmode compile/test/build-smoke when environment access is ready.

### 9. Source control is blocked by vendor asset classification
`Assets/Feel`, `Assets/Layer Lab`, and `Assets/ThirdParty.meta` are still untracked. These folders are large enough that staging them accidentally would make the repository noisy and risky.

Policy:
- Do not use `git add .` while vendor/import folders are unclassified.
- Each vendor asset needs: source/license note, owner, size review, commit/move/ignore decision, and import purpose.
- Only stage explicit files related to the current change.

Implementation direction:
- Create a vendor intake table before committing any third-party asset folder.
- Decide whether Feel/Layer Lab are production dependencies, temporary experiments, or local-only imports.

## Roadmap

### P0 - Stabilize Evidence
1. Run Unity compile and Play Mode smoke on current `main`.
2. Run F11 regression and release soak.
3. Record Stage 1-3 manual feel notes against the phase contract.
4. Classify untracked vendor assets before any broad staging.

Done when:
- A dated validation artifact exists for the current commit.
- No current work claims release readiness using stale logs.
- Vendor folders have an explicit source-control decision.

### P1 - Make Rhythm Playable
1. Add Release relief contract: one sensory relief plus one gameplay relief.
2. Add Build temptation rules for risk caches and breadcrumb momentum.
3. Add Spike tell and cue budget.
4. Add phase-aware exit decision: leave now, detour for reward, or risk a chase.

Done when:
- Calm, Build, Spike, and Release each change at least one player decision.
- Release is felt within 2 seconds without debug UI.
- Spike has a tell before the danger peak.

### P1 - Make Horror More Specific
1. Expand semantic audio roles and stinger budget.
2. Replace or reduce dummy generated loops for the highest-priority beats.
3. Define Korean-first player-facing action language.
4. Define the first visual motif set for enemy tell, route echo, corruption, relief, and death.

Done when:
- Major events are recognizable by sound and motion without reading a label.
- Debug language is hidden from normal player UI.
- Audio/visual feedback supports rhythm instead of becoming constant noise.

### P2 - Add Structure Around Future Content
1. Time set-pieces to Build-late or Spike-entry.
2. Add next-stage consequences from exit decisions.
3. Add CI/static preflight release gates.
4. Replace placeholder presentation in content batches rather than one-off effects.

Done when:
- New content enters through the same phase contract and validation gate.
- Set-pieces increase rhythm variety without breaking fairness.

## Candidate Tickets

1. Phase-aware risk cache wager
   - Files: `Assets/_Project/Scripts/Map/RiskCachePickup.cs`, `Assets/_Project/Scripts/Map/StageLoopDirector.cs`
   - Goal: scale cache reward, noise, and aftershock by rhythm phase.
   - Acceptance: Build caches are tempting, Spike caches are dangerous but clutch, Release caches support recovery/info.

2. Release relief contract
   - Files: `Assets/_Project/Scripts/Managers/GameplayRhythmDirector.cs`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs`, `Assets/_Project/Scripts/FogOfWarSystem.cs`
   - Goal: make Release visible and playable through at least two non-text channels.
   - Acceptance: playtesters can identify Release within 2 seconds without debug UI.

3. Breadcrumb momentum as route clarity
   - Files: `Assets/_Project/Scripts/Map/BreadcrumbPickup.cs`, `Assets/_Project/Scripts/Map/StageLoopDirector.cs`
   - Goal: let streaks improve next signal clarity, cache/exit hinting, or next Release relief.
   - Acceptance: breadcrumb chain creates route decisions, not just stamina gain.

4. Spike cue budget
   - Files: `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs`, `Assets/_Project/Scripts/Managers/AudioManager.cs`
   - Goal: prevent unfair stacking of stingers, camera, fog, and enemy aggression.
   - Acceptance: every Spike has a tell, and no more than one major stinger fires within 3 seconds.

5. Player-facing language split
   - Files: `Assets/_Project/Scripts/UI/GameplayFlowGuideRuntime.cs`, `Assets/_Project/Scripts/UI/GameplayHudRuntime.cs`, `Assets/_Project/Scripts/UI/EventFeedbackRuntime.cs`
   - Goal: keep debug terminology in debug overlay and make normal UI action-first.
   - Acceptance: normal play UI contains no raw pressure telemetry or mixed debug phase language.

6. Validation artifact refresh
   - Files: `Assets/_Project/Docs/VALIDATION_AND_ASSET_REVIEW_2026-05-18.md`, `Tools/RunStaticPreflight.ps1`, release soak logs
   - Goal: create fresh evidence for current `main`.
   - Acceptance: current commit has compile, Play Mode, F11, soak, and Stage 1-3 notes.

## Immediate Next Step Recommendation
The best next implementation step is the Release relief contract. It is small enough to validate quickly, directly supports horror rhythm, and gives the player a clear emotional beat after pressure. After that, implement Build temptation through risk cache and breadcrumb momentum tuning.

Suggested sequence:
1. Implement Release relief contract.
2. Run Play Mode smoke and manual Stage 1 feel test.
3. Implement Build temptation for risk caches and breadcrumbs.
4. Add Spike cue budget.
5. Refresh validation artifact and source-control asset decision.
