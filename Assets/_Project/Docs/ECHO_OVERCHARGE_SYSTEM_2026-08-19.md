# Echo Overcharge System - 2026-08-19

## Intent
Adds a repeatable risk-versus-information decision to the existing Q echo pulse. No second resource bar. Does not increase combat power.

## Player contract
- Tap Q: existing echo pulse at normal reveal/noise.
- Hold Q: build charge after a short tap grace window.
- Release Q: cast at current charge.
- Full charge auto-casts. Charge cannot be stored.

## Default full-charge scaling
- Reveal radius up to 1.65x (fog + scout information).
- Noise loudness/radius up to 1.80x.
- Resonance tail up to +2 pulses, cap 5.
- Stun radius and stun duration unchanged.

## Fairness
- Information and danger scale together.
- Charge rings shift echo-blue toward warning-red before the noisy cast.
- HUD shows Korean charge percent while holding (`Q 과충전 N%`).
- Smoke may reduce base noise; overcharge still multiplies remaining noise.
- Do not retune charge feel from automation while rhythm evidence is `NO_EVIDENCE`.

## Implementation status (2026-08-23)
Shipped on this pass:
- Input path (`GetKeyDown` / hold / `GetKeyUp` / full-charge auto-cast)
- Charge preview rings
- Reveal / noise / tail scaling
- HUD charge percent
- Overcharge telemetry (`OverchargeCastCount`, `FullChargeAutoCastCount`, `LastPulseCharge01`)
- Low-touch regression contract `Echo.OverchargeContract`
- Static preflight `code.echoOverchargeHooks`

Still open:
- Unity Editor compile confirmation
- Play Mode feel check (tap grace / charge build seconds stay structural defaults until a person or snapshot says otherwise)
- Echo-in-smoke reveal-for-noise trade (stability item 5; not started)

## Structural defaults (do not retune without Play Mode evidence)
- `tapGraceSeconds = 0.16`
- `chargeBuildSeconds = 0.85`
