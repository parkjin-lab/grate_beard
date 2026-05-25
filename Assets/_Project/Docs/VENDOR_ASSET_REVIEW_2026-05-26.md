# Vendor Asset Review - 2026-05-26

## Decision
Keep the current vendor imports local and ignored until the team approves license scope, package size, and the exact in-game use cases.

## Current Imports
| Path | Approx Size | File Count | Notes | Current Policy |
| --- | ---: | ---: | --- | --- |
| `Assets/Feel` | 414 MB | 4,928 | More Mountains Feel package with MMFeedbacks, MMTools, Nice Vibrations, demos, haptics, plugins, and third-party license notices | Ignore pending approval |
| `Assets/Layer Lab` | 261 MB | 9,918 | GUI Pro-FantasyRPG package with UI textures, fonts, scripts, demo scene, and external font license links | Ignore pending approval |
| `Assets/ThirdParty.meta` | small | 1 | Orphan folder meta without the reviewed folder content in source control | Ignore pending approval |

## Why This Is Blocked
- Both folders appear to be Asset Store/vendor packages rather than project-authored source.
- The combined import is roughly 707 MB before Unity Library processing.
- The folders include demos, fonts, binary plugins, haptics, media, and package-specific license notices.
- The project does not yet have a documented requirement that needs the entire packages in source control.

## Approval Checklist
Before committing any part of these imports:
1. Confirm the purchased license and the account/team that owns it.
2. Identify the exact runtime feature that needs the package.
3. Remove demos, unused pipelines, sample scenes, and unused media before staging.
4. Verify binary plugins are needed for the target platforms.
5. Record third-party font/plugin obligations in the resource document.
6. Run Unity compile after the scoped import.

## Recommended Use
- For screen shake, pulses, haptics, and feedback sequencing, first prefer existing lightweight project scripts.
- If Feel is approved, import only the minimum runtime modules needed for feedback authoring.
- If Layer Lab is approved, extract only the UI sprites/fonts that match the horror HUD direction.
- Do not use either package as a general dumping ground for visual identity. The game still needs a small bespoke horror motif kit.

## Git Policy Added
The following paths are now ignored to prevent accidental commits:
- `Assets/Feel/`
- `Assets/Feel.meta`
- `Assets/Layer Lab/`
- `Assets/Layer Lab.meta`
- `Assets/ThirdParty.meta`
