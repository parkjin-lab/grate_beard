# Rhythm Design Review
Updated: 2026-05-10

## Design Read
- The game already had useful micro-rhythm: echo return windows, safe haven overstay pressure, stage 3/5/7 set-pieces, pressure-wave feedback, and a dread audio layer.
- The missing piece was macro-rhythm. Stage pressure mostly climbed or reacted continuously, so the player could feel danger, but not a designed breath pattern.
- The target rhythm is Calm -> Build -> Spike -> Release. This gives exploration a pulse: read space, feel pressure gather, survive a short crest, then get a small exhale before the next cycle.

## Runtime Update
- Added `GameplayRhythmDirector` as a central rhythm conductor.
- `StagePressureDirector` now lets the rhythm phase shape runtime pressure, while regression runs keep deterministic pressure checks.
- `AudioDummyLoopRuntime` now uses rhythm tempo/intensity to modulate pitch and dread drone tension.
- Debug overlay now exposes phase, tempo, intensity, pressure multiplier, and cycle count.
- Regression checklist now checks that the rhythm director exists, is enabled, exposes telemetry, and has a proper Calm/Build/Spike/Release pressure shape.

## Next Design Checks
- Playtest whether Calm gives enough route-reading time before Build begins.
- Tune Spike duration for fear without making it feel unfair.
- Make Release more legible through lighting, room tone, or enemy search behavior.
- Consider tying set-piece timing to rhythm phase so stage events land during Build or Spike instead of feeling isolated.
