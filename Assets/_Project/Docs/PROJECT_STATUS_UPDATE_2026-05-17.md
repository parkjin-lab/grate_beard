# 프로젝트 상황 업데이트
Updated: 2026-05-17

## 현재 상태 요약
- 현재 브랜치는 `main`이며, `origin/main`보다 1커밋 앞서 있다.
- HEAD는 `ffb0f8e Checkpoint horror loop and stability work`이다.
- 현재 변경은 리듬 시스템, 공포 루프, 저장 안정화, 회귀 체크, HUD/피드백, `SampleScene` wiring에 걸쳐 있다.
- 핵심 신규 파일은 `GameplayRhythmDirector.cs`이며, 아직 미추적 상태다.
- 2026-05-10 문서들과 이번 2026-05-17 문서들은 아직 미추적 상태다.
- 대형 미추적 폴더가 있다: `Assets/Feel`, `Assets/Layer Lab`, `Assets/ThirdParty`, `Assets/_Recovery`.

## 최근 구현 축
1. 리듬 시스템
   - `GameplayRhythmDirector`가 Calm -> Build -> Spike -> Release 매크로 리듬을 담당한다.
   - `StagePressureDirector`는 런타임 압박을 리듬 페이즈로 변조한다.
   - 회귀 실행 중에는 결정성 유지를 위해 리듬 압박 변조를 피한다.
   - `AudioDummyLoopRuntime`은 리듬 tempo/intensity를 BGM/ambience pitch와 dread layer tension에 반영한다.
   - `DebugOverlay`, HUD/feedback, `RegressionChecklistRunner`에 rhythm telemetry와 `RhythmShift` 이벤트가 연결됐다.
   - `SampleScene`에 `GameplayRhythmDirector`가 직접 추가되고 주요 참조가 연결됐다.

2. 공포 루프 방향
   - 방향성은 단순한 압박 상승이 아니라 `읽기 -> 위험 감수 -> 생존 -> 회복`의 호흡 있는 루프로 이동했다.
   - Spike는 압박 증가와 카메라 impulse를 제공한다.
   - Release는 수치상 완화는 있지만, 플레이어가 체감하는 안도/보상 표현은 아직 약하다.
   - 다음 설계 초점은 Build의 유혹과 Release의 체감 보상이다.

3. 저장/로드 안정화
   - `SaveManager`는 load/new-run 시 저장하지 않아야 할 transient 상태를 정리하도록 확장됐다.
   - 대상은 temporary quiet breath, concealment, flashlight dread modifiers, pulse/decoy/smoke runtime state, readability transient tuning이다.
   - `RegressionChecklistRunner`에 load/new-run 후 transient cleanup 검증이 추가됐다.
   - `StageLoopDirector`는 locked exit이 distant active breadcrumb 안내를 훔치지 않도록 조정됐다.

## 검증 상태
- 이번 무인 처리 기준 `git diff --check`는 통과했다.
- 로컬 Roslyn/Unity response file 기반 C# compile은 `GameplayRhythmDirector.cs`를 명시 포함한 상태로 통과했다.
- `EnemyMovementEchoVisual`/`EnemyController`의 project code warning은 정리되어, 현재 CLI compile 출력은 Unity source generator analyzer load warnings만 남는다.
- merge conflict marker scan은 통과했고, `RhythmShift`/`GameplayRhythmDirector` 참조는 예상 범위 안에 있다.
- Unity Editor compile은 아직 미확인이다.
- Play Mode smoke test는 아직 미확인이다.
- F11 regression은 아직 미확인이다.
- release soak with report file은 아직 미확인이다.
- stage 1~3 수동 플레이테스트는 아직 미확인이다.

## 주요 위험
- `GameplayRhythmDirector.cs`가 미추적 상태라 커밋 시 빠지면 `SampleScene` 참조가 깨진다.
- `SampleScene.unity`가 직접 수정되어 병렬 작업 충돌 가능성이 높다.
- 대형 미추적 폴더 때문에 `git add .` 사용은 위험하다.
- `SaveManager.HandleMapGenerated` autosave는 새 stage rhythm/pressure 재계산보다 먼저 checkpoint를 저장할 수 있어 한 프레임 지연 저장 패치가 필요하다.
- 리듬 압박, set-piece, enemy density, Spike camera impulse가 겹치면 불공정한 난이도 스파이크가 발생할 수 있다.
- 회귀 중 리듬 압박 변조가 비활성화되므로, 자동검증 통과가 실제 체감 안정성을 완전히 보장하지 않는다.

## 이번 무인 처리에서 반영한 안정화
- `RegressionChecklistRunner.RunReleaseCandidateSoakRoutine()`도 회귀 실행 플래그를 켜도록 수정했다.
- 이제 F11 checklist뿐 아니라 release soak 중에도 리듬 phase 업데이트와 압박 변조가 멈춰, soak/matrix 결과가 리듬 타이밍에 따라 흔들릴 가능성이 줄었다.
- `GameplayRhythmDirector.EnterPhase()`는 새 phase의 pressure/readability 적용 후 duration을 계산해 phase 전환 직후의 한 박자 늦은 context sampling을 줄였다.
- `StagePressureDirector.ApplySavedPressureStateForRuntime()`은 저장된 base pressure가 있으면 현재 rhythm phase 기준으로 total pressure와 파생 multiplier를 재계산한다.
- `AudioDummyLoopRuntime`은 기본적으로 generated fallback loop만 자동 재생하고, 실제 assigned clip 자동 재생은 별도 옵션으로 분리했다.
- `DebugOverlay` main/regression panel은 작은 Game view에서도 화면 안으로 clamp되고, main panel 스크롤과 regression overlap 방지가 추가됐다.
- `GameplayRhythmDirector`/`StagePressureDirector`는 `MapSystem` late resolve 시에도 `MapGenerated` 이벤트 구독을 보장한다.
- `RunLoadoutDirector.TrySelectLoadoutById()`는 locked/unavailable loadout을 실제 적용하지 못한 경우 실패로 반환한다.
- `RegressionChecklistRunner` release soak는 disk/event suppression을 유지하면서 save/load/new-run 검증 구간에서만 runtime save mutation guard를 임시로 열고, 예외 시 save snapshot을 finally에서 복원한다.
- `EnemyMovementEchoVisual`은 `collider2D` 필드명 충돌과 `FindObjectOfType` obsolete warning을 정리했다.
- `EnemyController`는 `Physics2D.CircleCastNonAlloc`을 Unity 6 권장 `CircleCast` overload로 교체했다.
- `SaveManager.cs`는 CP949-valid/UTF-8-invalid 상태라 `apply_patch` 편집이 실패했고, byte-preserving patch 시도는 OS write denial로 보류됐다. autosave defer는 다음 안전 편집/인코딩 정리 시 처리해야 한다.

## 판단
현재 프로젝트는 "리듬 시스템 구현 완료"가 아니라 "리듬 시스템 검증 대기" 상태다. 다음 작업은 새 기능 구현보다 Unity 런타임 안정성 증명과 짧은 플레이테스트가 우선이다.
