# Next Work Checklist
Updated: 2026-05-10

## P0 - Prove The Rhythm Build
1. Unity compile in Editor.
   - Goal: Console has no compile errors after `GameplayRhythmDirector`, `RuntimeEventSemantic.RhythmShift`, scene references, and regression checks import.
   - Watch: Unity may regenerate script imports because the new script was not in the previous Bee response file until reimport.

2. Scene wiring smoke test.
   - Confirm `GameplayRhythmDirector` exists and is enabled in `SampleScene`.
   - Confirm `StagePressureDirector`, `AudioDummyLoopRuntime`, `ThreatReadabilityDirector`, `RegressionChecklistRunner`, and `DebugOverlay` resolve rhythm references at runtime.
   - Confirm DebugOverlay shows phase, progress, tempo, intensity, pressure multiplier, and cycle count.

3. F11 regression with rhythm checks.
   - Required pass: `Rhythm.Enabled`, `Rhythm.PressureShape`, `Rhythm.Telemetry`.
   - Also verify existing pressure curve, objective loop, chase readability, death reset, and transient reset checks.

4. Release soak pass.
   - Recommended order: Auto soak preflight -> trace status -> release soak with report file.
   - Required pass: save/load, new-run, death reset, matrix, chase readability, rhythm, transient cleanup.

5. Short manual playtest.
   - Play one run through at least stage 3.
   - Check whether Calm gives route-reading time, Build raises tension without confusion, Spike feels scary but fair, and Release is actually felt.

## P1 - Make Rhythm Into Decisions
1. Release readability.
   - Add or tune a clear exhale: objective whisper, brief stamina relief, lighter room tone, weaker fog dread, or enemy search hesitation.

2. Build temptation.
   - Make risk cache, corrupted breadcrumb, exit cache, or breadcrumb momentum most attractive during Build.
   - Build should make the player think, "I can still grab one more thing."

3. Spike fairness.
   - Add a readable pre-spike tell near the end of Build.
   - Avoid spawning brand-new unfair problems during Spike; let prior choices pay off or punish.

4. Set-piece phase timing.
   - Move stage 3/5/7 set-piece beats toward Build-late or Spike entry after P0 validation.

5. HUD/audio noise budget.
   - Keep phase names in debug only.
   - Let players feel rhythm through sound, fog, camera, threat motion, and cue quality.

## P2 - Content/Presentation Follow-Up
1. Replace remaining dummy visuals with first-pass readable assets.
2. Add distinct SFX language for EchoReturn, EscapeRelief, BreathBroken, RhythmShift, ExitUnlocked, and SetPieceShift.
3. Review Korean/English HUD text consistency and prevent debug-language leakage into the player surface.
4. Revisit untracked asset folders and decide what belongs in source control.
