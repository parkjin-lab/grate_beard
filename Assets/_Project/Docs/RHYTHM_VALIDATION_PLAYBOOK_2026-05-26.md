# Rhythm Validation Playbook - 2026-05-26

## Purpose
Validate that the game is not only harder or darker, but rhythmically readable: Calm -> Build -> Spike -> Release should be visible, playable, and emotionally different.

## In-Game Debug Support
- `DebugOverlay` now shows `Rhythm Validation` when a `GameplayRhythmDirector` is present.
- The gate marks Calm, Build, Spike, and Release after each phase remains active for at least 0.75 seconds.
- Press `F9` to reset the phase observation gate during a fresh test run.
- A pass only proves that all phases occurred; it does not prove they felt good. Use the manual notes below for that.

## 10-Minute Manual Pass
| Timebox | What To Watch | Pass Signal | Fail Signal |
| --- | --- | --- | --- |
| 0-2 min | Calm entry and first scan | Player has space to read the room and choose a direction | Player is pressured before understanding the room |
| 2-4 min | Build phase | Reward, breadcrumb, or exit lure makes a risky route tempting | Pressure rises without creating a decision |
| 4-6 min | Spike phase | A short, unmistakable threat crest changes movement or ability use | Spike feels random, invisible, or too long |
| 6-8 min | Release phase | Player gets a brief breath, recovery, or route clarity | Relief is invisible or immediately cancelled |
| 8-10 min | Full cycle repeat | The next cycle feels related but not identical | The rhythm becomes flat or purely numeric |

## Screenshot/Capture Targets
Capture one still or short clip for each state:
- Calm: widest safe read, low pressure, clear route choice.
- Build: visible temptation or route risk.
- Spike: strongest threat cue, no text dependency.
- Release: pressure drop, recovery cue, or safe route reveal.

## Semantic Audio Spot Check
During the same pass, watch `Event Audio Stinger Last` in `DebugOverlay`.
Before the pass, use the `AudioManager` context menu to audition the main stingers without waiting for every gameplay event.

| Moment | Expected Semantic Stinger | What To Confirm |
| --- | --- | --- |
| Exit opens | `ExitUnlocked` | It cuts through without hiding other danger cues |
| Chase starts or spike lands | `ChaseSpike` or `LockOnWarning` | It feels urgent and short |
| Echo draws danger back | `EchoReturn` | It reads as consequence, not reward |
| Risk cache is collected | `RiskReward` | It sounds tempting but unsafe |
| Release begins | `EscapeRelief` or `RhythmShift` | It briefly lowers tension without feeling like full safety |
| Quiet breath breaks | `QuietBreathBroken` | It clearly tells the player they spent their relief window |

Generated placeholder tones now have stronger cue shapes:
- `LockOnWarning`: repeated low warning knocks before the threat arrives.
- `ChaseSpike`: short broken burst with a heavier tail.
- `EscapeRelief`: slower rising phrase that should feel like an exhale, not a victory.
- `QuietBreathBroken`: sharp snap into a low falloff.
- `RhythmShift`: small pulse pattern for phase change awareness.

## Notes Template
Use this compact format while testing:

```text
Stage:
Loadout:
Cycle:
Calm read:
Build decision:
Spike cause:
Release relief:
Moment that felt best:
Moment that felt unfair:
Needed resource:
Needed tuning:
```

## Immediate Tuning Questions
- Does the player understand why pressure is rising?
- Does the spike create action, or only visual noise?
- Is release long enough to be felt, but short enough to keep dread?
- Are semantic audio cues distinct enough without watching the debug overlay?
- Does darkness increase focus, or simply hide important information?
