# 리듬 작업 이후 핸드오프
Updated: 2026-05-17

## 바로 시작할 순서
1. `git status --short`
2. `git diff --check`
3. Unity Editor compile
4. `SampleScene` Play Mode smoke test
5. DebugOverlay rhythm telemetry 확인
6. F11 regression
7. release soak + report file
8. stage 1~3 수동 플레이테스트

## 현재 변경 범위
- 수정된 tracked 파일은 `SampleScene.unity`, AI warning cleanup, project setup, runtime event, pressure/audio/regression/save/readability/stage loop/HUD/debug 관련 파일이다.
- 신규 핵심 미추적 파일은 `GameplayRhythmDirector.cs`와 2026-05-10/2026-05-17 문서들이다.
- 대형 미추적 폴더는 `Assets/Feel`, `Assets/Layer Lab`, `Assets/ThirdParty`, `Assets/_Recovery`다.
- `git add .`를 사용하지 말고 스테이징 범위를 수동 지정해야 한다.

## 반드시 확인할 것
| Check | Status | Note |
| --- | --- | --- |
| `git diff --check` | PASS at 2026-05-17 doc update | 공백/충돌 표식 없음 |
| Roslyn/Unity response compile | PASS at 2026-05-17 warning cleanup pass | `GameplayRhythmDirector.cs` 명시 포함, project code warnings cleared; Unity analyzer load warnings only |
| Conflict marker scan | PASS at 2026-05-17 autonomous pass | merge conflict marker 없음, rhythm 참조는 예상 범위 |
| Unity Editor compile | NOT VERIFIED | 최우선 |
| Play Mode | NOT VERIFIED | `SampleScene` 참조 확인 필요 |
| F11 regression | NOT VERIFIED | rhythm checks 포함 |
| Release soak | NOT VERIFIED | report file 필요 |
| Manual stage 1~3 playtest | NOT VERIFIED | 체감 검증 필요 |

## 위험요소
- `GameplayRhythmDirector.cs`가 빠지면 scene reference가 깨진다.
- `SampleScene.unity` 직접 수정은 병렬 작업 충돌 위험이 크다.
- Unity가 import 후 serialized field order를 다시 쓸 수 있다.
- F11/release soak 회귀 중 rhythm pressure modulation이 꺼져 있어, 자동검증 통과만으로 실제 체감이 검증되지 않는다.
- `SaveManager.HandleMapGenerated` autosave는 현재 한 프레임 지연되지 않아 새 stage의 rhythm/pressure 재계산보다 먼저 checkpoint를 잡을 수 있다.
- Spike가 pressure, camera impulse, set-piece, enemy density와 겹치면 불공정할 수 있다.
- Release는 아직 플레이어-facing relief가 약하다.

## 무인 처리 기록 - 2026-05-17
- 서브 에이전트 3개로 git scope, 문서/인코딩, 리듬 코드 위험을 분리 점검했다.
- `RunReleaseCandidateSoakRoutine()`에서도 `RegressionChecklistRunner.IsRegressionRunActive`를 켜도록 수정해 release soak 중 리듬 phase가 회귀 결과를 흔들지 않게 했다.
- 추가 서브 에이전트 3개로 pressure/readability, audio/debug, git staging manifest를 분리 점검했다.
- `GameplayRhythmDirector.EnterPhase()`가 새 phase 압박/가독성을 먼저 적용한 뒤 phase duration을 산정하도록 조정했다.
- `StagePressureDirector.ApplySavedPressureStateForRuntime()`은 저장된 base pressure가 있으면 현재 rhythm 기준으로 total pressure와 파생 multiplier를 재계산한다.
- `AudioDummyLoopRuntime`은 기본적으로 generated fallback clip만 자동 재생하고, assigned clip 자동 재생은 `autoPlayAssignedClips`로 분리했다.
- `DebugOverlay` main/regression panel rect를 현재 Game view 크기 안으로 clamp하고, main panel 스크롤과 overlap 방지를 추가했다.
- `GameplayRhythmDirector`/`StagePressureDirector`는 `MapSystem`이 늦게 resolve되어도 `MapGenerated` 구독을 다시 보장한다.
- `RunLoadoutDirector.TrySelectLoadoutById()`는 실제 선택 적용에 실패한 locked/unavailable loadout을 성공으로 보고하지 않는다.
- release soak는 disk/event suppression을 유지하면서 save/load/new-run assertions 구간에서만 runtime save mutation guard를 임시로 열고, 예외 시에도 save snapshot을 finally에서 복원한다.
- `SaveManager.HandleMapGenerated` autosave defer 패치는 필요성이 확인됐지만, `SaveManager.cs`가 현재 invalid UTF-8로 `apply_patch` 편집을 거부해 무인 처리에서는 보류했다.
- `EnemyMovementEchoVisual`/`EnemyController`의 Unity 6 obsolete/project-code compile warnings를 정리했다.
- `SaveManager.cs`는 CP949-valid/UTF-8-invalid 파일이며, byte-preserving patch 시도는 OS write denial로 보류됐다.
- 로컬 CLI 기준 `git diff --check`, rhythm reference/conflict scan, Roslyn response compile을 통과했다.
- Unity Editor compile, Play Mode, F11 regression, release soak report, stage 1~3 수동 체감 검증은 아직 남아 있다.

## 검증 후 바로 이어갈 구현
1. Release readability
   - objective whisper 강화, room tone 완화, fog dread 약화, 짧은 stamina relief, enemy search hesitation 중 하나를 작게 붙인다.

2. Build temptation
   - risk cache와 breadcrumb momentum을 Build에서 더 매력적으로 만든다.

3. Spike tell
   - Build 후반 또는 Spike 진입 직전에 오디오/안개/카메라/적 자세로 짧은 예고를 준다.

4. Set-piece phase timing
   - P0 검증 통과 후 stage 3/5/7 set-piece를 Build-late 또는 Spike-entry로 연결한다.

5. Validation result table 유지
   - 다음 작업자는 compile/play/F11/soak/manual 결과를 이 문서 또는 후속 문서에 한 줄씩 남긴다.
