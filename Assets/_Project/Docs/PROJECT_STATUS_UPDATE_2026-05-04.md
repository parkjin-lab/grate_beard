# 프로젝트 상태 업데이트 - 2026-05-04

작성자: Worker 1  
범위: 현재 git 상태, 기존 문서, 미커밋 코드 diff 확인 후 문서화. 코드 수정 없음.

## 1) 현재 브랜치 / Git 상태
- 브랜치: `main`, 추적 대상 `origin/main`.
- 문서 작성 전 `git status --short --branch`: tracked 코드 8개 수정, 신규 문서/패키지/복구 폴더 다수 untracked.
- tracked 코드 변경 요약: `git diff --stat` 기준 8 files, +1499 / -36.
- 수정된 tracked 코드:
  - `Assets/_Project/Scripts/Events/RuntimeEventBus.cs`
  - `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs`
  - `Assets/_Project/Scripts/Map/StageLoopDirector.cs`
  - `Assets/_Project/Scripts/Player/PlayerDummyController.cs`
  - `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs`
  - `Assets/_Project/Scripts/UI/DebugOverlay.cs`
  - `Assets/_Project/Scripts/UI/EventFeedbackRuntime.cs`
  - `Assets/_Project/Scripts/UI/GameplayHudRuntime.cs`
- untracked 폴더: `Assets/Feel`, `Assets/Layer Lab`, `Assets/MCPForUnity`, `Assets/ThirdParty`, `Assets/_Recovery` 및 일부 `.meta`.
- Worker 1 변경 파일: 이 문서만. Unity를 실행하지 않았고 `.meta`는 생성/수정하지 않음.

## 2) 최신 구현된 게임플레이 시스템
- Horror pacing 보강: `ThreatReadabilityDirector`가 기존 압박도에 더해 Dread Beat, Phantom Cue, Close Stalker Cue, flashlight dread, 카메라/안개/적 감지 튜닝을 계속 관리.
- Escape Relief: 일정 시간 이상 추격 후 `ChaseDisengaged`가 발생하면 스태미나 회복, 안개 reveal, 초록 relief pulse, 적 방향 trail, 다음 목표 whisper, 짧은 calm window가 발동.
- Quiet Breath: Escape Relief 보상 중 플레이어에게 임시 소음 억제 버프 적용. 발소리/스프린트 소음 배율이 낮아지고 HUD 스태미나 라인에 남은 시간이 표시됨.
- Breath Snap: Quiet Breath 상태에서 스프린트를 오래 유지하면 calm window가 줄고, 주황 pulse/오디오/카메라 impulse/소음 방출과 `QuietBreathBroken` 이벤트가 발생.
- Echo Resonance Tail: Echo Pulse 사용 후 여러 차례 잔향 pulse가 뒤따라 fog reveal과 약한 echo noise를 남김.
- Echo Return: 잔향 마지막 tick에서 주변 위협을 찾으면 적 방향 hint line/pulse, 위협 수/거리 기록, `EchoReturn` 이벤트와 HUD `Q 응답` 표시가 뜸.
- HUD/debug 피드백: priority cue에 `BREATH FOUND`, `BREATH BROKE`, `ECHO RETURN` 추가. DebugOverlay는 BreathSnap strain/cooldown, Quiet Breath 상태, Echo Resonance/Return 정보를 표시.
- Objective handoff: `StageLoopDirector.TryGetNextObjectiveTarget`가 추가되어 Escape Relief objective whisper가 다음 breadcrumb 또는 exit 방향을 받을 수 있음.

## 3) 검증 상태
- 실행한 검증:
  - `git diff --check`: 출력 없음. 현재 diff의 whitespace/error marker 검사는 통과.
  - 기존 문서 확인: `PROJECT_STATUS_NEXT_BASELINE.md`, `BASELINE_CALIBRATION_PLAYBOOK.md`, `UX_UI_FUN_DIRECTING_STRATEGY_2026-04-08.md`, `RESOURCE_REQUIREMENTS_CURRENT_STAGE_2026-04-23.md`.
  - 관련 코드 diff/키워드 확인: Escape Relief, Quiet Breath, Breath Snap, Echo Resonance Tail, Echo Return, HUD/debug feedback.
- 미실행 검증:
  - Unity Editor compile/Play Mode.
  - F11 regression matrix, release soak, stage 1/3/5 preset matrix.
  - 실제 플레이 감각 검증: 추격 해제 직후 relief가 과하게 자주/강하게 느껴지는지, Breath Snap이 벌칙으로 읽히는지 확인 필요.

## 4) 알려진 리스크 / 제한
- `ThreatReadabilityDirector.cs` 변경량이 매우 큼. 여러 pacing/VFX/audio/HUD 상태가 한 클래스에 묶여 있어 회귀 발생 시 원인 분리가 어려울 수 있음.
- Escape Relief는 `ChaseStarted`/`ChaseDisengaged` 이벤트 품질에 강하게 의존. 적이 여러 마리일 때 `activeChaseEventCount`가 어긋나면 보상 타이밍이 늦거나 누락될 수 있음.
- Quiet Breath와 Breath Snap은 `Time.time`/`Time.realtimeSinceStartup`을 함께 사용한다. pause, timescale, load/reset 경로에서 잔여 시간이 의도대로 정리되는지 확인 필요.
- Echo Return은 활성 적 목록과 거리 기반 hint를 사용한다. 벽/시야 차단을 고려하지 않으므로 "위협 방향 힌트"가 지나치게 정답처럼 보일 수 있음.
- 런타임 VFX/audio 오브젝트와 material/clip을 코드에서 생성한다. 긴 플레이/반복 리셋에서 누수나 hierarchy 잔여물이 없는지 확인 필요.
- `RESOURCE_REQUIREMENTS_CURRENT_STAGE_2026-04-23.md`는 현재 인코딩이 깨져 읽기 어렵다. 리소스 요구사항은 별도 원본 확인이 필요.
- 대량 untracked asset/package 폴더가 존재한다. 다른 worker 작업일 수 있으므로 이번 문서에서는 판단/정리하지 않음.

## 5) 즉시 핸드오프 노트
- 먼저 Unity compile을 확인하고, 에러가 나면 `ThreatReadabilityDirector`, `PlayerEchoPulseAbility`, `PlayerDummyController` 순서로 public API/namespace 충돌을 본다.
- Play Mode에서 짧은 루프:
  - 적에게 추격을 유도한 뒤 2.4초 이상 버티고 추격 해제.
  - `BREATH FOUND` cue, 스태미나 회복, 초록 pulse, 목표 whisper, HUD `숨 Ns` 표시 확인.
  - relief 중 sprint 유지 후 `BREATH BROKE`, 소음 발생, 주황 pulse, debug strain/cooldown 확인.
  - Q Echo Pulse 사용 후 `Q 잔향`, 마지막 위협 감지 시 `Q 응답 거리`, `ECHO RETURN` cue 확인.
- Regression은 기존 playbook 기준으로 F11 matrix와 release soak를 다시 돌려야 함. 이번 문서 작성자는 Unity를 실행하지 않았음.
- `.meta`는 아직 만들지 않았으므로 Unity가 문서를 import하면 `PROJECT_STATUS_UPDATE_2026-05-04.md.meta`가 생성될 수 있음.
