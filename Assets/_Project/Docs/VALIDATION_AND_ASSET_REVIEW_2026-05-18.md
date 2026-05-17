# Validation And Asset Review
Updated: 2026-05-18

## Validation Attempt
- Local Roslyn/Unity response compile: PASS.
- Remaining CLI output is Unity source generator analyzer load warnings only.
- Unity batchmode compile attempt returned exit code 0, but no requested log file was produced.
- Running Unity editor processes were already present, so they were not terminated or disturbed.
- Unity MCP editor state/log query was unavailable.

## Still Not Verified
- Unity Editor Console compile status.
- `SampleScene` Play Mode smoke test.
- DebugOverlay rhythm telemetry in Play Mode.
- F11 regression.
- Release soak report file.
- Stage 1-3 manual rhythm feel test.

## Untracked Asset Scope
| Path | Files | Size | Recommendation |
| --- | ---: | ---: | --- |
| `Assets/Feel/` | 4,928 | 433,710,814 bytes | Decide as external/vendor asset import before staging. |
| `Assets/Layer Lab/` | 9,918 | 273,292,718 bytes | Decide as external/vendor asset import before staging. |
| `Assets/_Recovery/` | 4 | 3,422,395 bytes | Treat as Unity recovery output; inspect before keeping. |
| `Assets/ThirdParty.meta` | n/a | n/a | Keep only if the matching folder is intentionally part of project layout. |

## Source Control Guardrail
- Do not use `git add .` while these folders remain unclassified.
- Current pushed code/docs are already on `origin/main` at `ea213f0`.
- Next source-control decision should be only about the large untracked asset/recovery items.
