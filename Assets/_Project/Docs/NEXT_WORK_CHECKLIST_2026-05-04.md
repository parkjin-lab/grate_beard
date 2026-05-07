# LostBreadcrumbs 다음 작업 체크리스트

Updated: 2026-05-04

## 작성 기준
- 이 문서는 코드 수정 없이 현재 문서, 미커밋 변경, 주요 런타임 스크립트를 읽고 정리한 다음 작업용 체크리스트다.
- 기존 기준 문서: `Assets/_Project/Docs/PROJECT_STATUS_NEXT_BASELINE.md`, `Assets/_Project/Docs/BASELINE_CALIBRATION_PLAYBOOK.md`, `Assets/_Project/Docs/UX_UI_FUN_DIRECTING_STRATEGY_2026-04-08.md`.
- 현재 미커밋 코드 변경으로 `RuntimeEventBus`, `ThreatReadabilityDirector`, `StageLoopDirector`, `PlayerDummyController`, `PlayerEchoPulseAbility`, `DebugOverlay`, `EventFeedbackRuntime`, `GameplayHudRuntime`가 변해 있다. 다른 작업자의 변경일 수 있으므로 후속 작업자는 먼저 diff를 다시 확인한다.
- 외부/복구성 untracked 폴더(`Assets/Feel`, `Assets/Layer Lab`, `Assets/MCPForUnity`, `Assets/ThirdParty`, `Assets/_Recovery`)가 있다. 이 체크리스트는 해당 폴더를 작업 범위로 삼지 않는다.

## P0 - 검증 및 안정화

| ID | 작업 | 주요 참조 | 완료 기준 |
| --- | --- | --- | --- |
| P0-1 | Unity 컴파일 및 기본 씬 구성 무결성 확인 | `Assets/_Project/Scripts/Editor/LostBreadcrumbsProjectSetup.cs:777`, `Assets/_Project/Scripts/Events/RuntimeEventBus.cs:18` | Unity Console에 compile error가 없다. `LostBreadcrumbs/Setup/Build Full Playground` 실행 후 `MapSystem`, `StageLoopDirector`, `RegressionChecklistRunner`, `GameplayHudRuntime`, `EventFeedbackRuntime` 참조 누락 경고가 없다. 새 이벤트 semantic(`EscapeRelief`, `QuietBreathBroken`, `EchoReturn`)이 구독자 없이도 예외를 내지 않는다. |
| P0-2 | 회귀 게이트를 릴리즈 후보 기준으로 재확인 | `Assets/_Project/Scripts/Managers/RegressionChecklistRunner.cs:316`, `Assets/_Project/Scripts/Managers/RegressionChecklistRunner.cs:395`, `Assets/_Project/Scripts/Managers/RegressionChecklistRunner.cs:465`, `Assets/_Project/Scripts/Editor/LostBreadcrumbsProjectSetup.cs:1276` | Play Mode에서 `F11` 런타임 체크가 PASS다. Compact/Standard/Expansive x stage 1/3/5 matrix가 PASS다. Chase readability regression이 PASS다. `Apply Release Checklist Freeze Defaults` 후 overlay의 `Release Checklist Gate`가 ready 상태다. `F2` 또는 `Run Release Soak + Write Report File (Auto)`가 disk write suppression 상태로 PASS 리포트를 남긴다. |
| P0-3 | Echo Return/Resonance Tail 안정화 | `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs:35`, `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs:102`, `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs:169`, `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs:251`, `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs:369` | Q 펄스 사용 시 기본 reveal/stun/noise 뒤 resonance tail이 정해진 횟수만 실행된다. 마지막 tail에서 실제 위협이 범위 안에 있을 때만 `EchoReturn` 이벤트와 HUD `Q 응답` 라인이 뜬다. 오브젝트 disable, death reset, new run 후 coroutine/VFX가 남지 않는다. RegressionChecklist 실행 중에는 입력 freeze와 저장 억제가 유지된다. |
| P0-4 | Escape Relief/Quiet Breath 안정화 | `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:146`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:1126`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:1153`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:1632`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:1665` | 추격이 최소 시간 이상 지속된 뒤 완전히 disengage되면 stamina 회복, fog reveal, enemy trail, objective whisper, calm pressure dip, `EscapeRelief` 이벤트가 한 번만 발생한다. cooldown 안에서는 보상이 중복되지 않는다. quiet breath 중 sprint로 strain threshold를 넘기면 `QuietBreathBroken` 이벤트, breath snap VFX/SFX, calm window 단축, noise 방출이 발생한다. 회귀 실행 중에는 relief/breath side effect가 억제된다. |
| P0-5 | HUD/피드백 이벤트 예산 확인 | `Assets/_Project/Scripts/UI/GameplayHudRuntime.cs:324`, `Assets/_Project/Scripts/UI/GameplayHudRuntime.cs:646`, `Assets/_Project/Scripts/UI/EventFeedbackRuntime.cs:440`, `Assets/_Project/Scripts/UI/EventFeedbackRuntime.cs:451`, `Assets/_Project/Scripts/UI/DebugOverlay.cs:376` | `BREATH FOUND`, `BREATH BROKE`, `ECHO RETURN`, `CHASE STARTED`, `EXIT OPEN` priority cue가 겹쳐 읽기 어려운 상태로 쌓이지 않는다. alert feed duplicate suppression이 같은 semantic 반복을 억제한다. HUD의 한글/영문 혼합 문구가 플레이 중 의미를 잃지 않는다. 16:9, 21:9, 최소 창 크기에서 HUD/flow guide/priority cue가 서로 겹치지 않는다. |
| P0-6 | 저장/로드/새 런 상태 불변식 재검증 | `Assets/_Project/Scripts/Managers/SaveManager.cs:388`, `Assets/_Project/Scripts/Managers/SaveManager.cs:501`, `Assets/_Project/Scripts/Managers/SaveManager.cs:576`, `Assets/_Project/Scripts/Managers/SaveManager.cs:608`, `Assets/_Project/Scripts/Managers/SaveManager.cs:1007` | QuickSave/QuickLoad 후 stage, player position, health, stamina, flashlight, telemetry, pressure, readability 값이 복원된다. temporary quiet breath, echo return warning, active VFX 같은 순간 효과는 로드/새 런 뒤 부적절하게 지속되지 않는다. Release soak의 save snapshot restore가 실제 save 파일을 더럽히지 않는다. |
| P0-7 | 목표 안내/탈출 루트 힌트 안정화 | `Assets/_Project/Scripts/Map/StageLoopDirector.cs:29`, `Assets/_Project/Scripts/Map/StageLoopDirector.cs:41`, `Assets/_Project/Scripts/Map/StageLoopDirector.cs:125`, `Assets/_Project/Scripts/Map/StageLoopDirector.cs:680`, `Assets/_Project/Scripts/Map/StageLoopDirector.cs:1047` | breadcrumb chain echo는 다음 breadcrumb 또는 열린 exit를 일관되게 가리킨다. stage 5+ corrupted breadcrumb echo가 의도된 혼란을 만들되 실제 objective hint와 색/타이밍으로 구분된다. exit unlock pressure가 한 번만 실행되고, `ExitUnlocked` 이벤트와 beacon/noise가 같은 순간에 이해 가능하게 읽힌다. |

## P1 - 게임플레이 루프 개선

| ID | 작업 | 주요 참조 | 완료 기준 |
| --- | --- | --- | --- |
| P1-1 | Echo-Relief 의사결정 루프 튜닝 | `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs:188`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:1187`, `Assets/_Project/Scripts/Managers/StagePressureDirector.cs:140` | Echo는 정보를 주지만 noise risk를 만든다. Escape Relief는 추격 탈출 보상으로 느껴지되 무한 sustain이 되지 않는다. stage 1에서는 학습 가능하고, stage 3에서는 선택 압박이 생기며, stage 5에서는 잘못 쓴 Echo/스프린트가 명확한 리스크로 돌아온다. |
| P1-2 | 압박-휴식 beat window 정리 | `Assets/_Project/Scripts/Managers/StagePressureDirector.cs:116`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:455`, `Assets/_Project/Scripts/Managers/StageSetPieceDirector.cs:256` | 각 stage에는 최소 1번의 low-pressure breath와 1번의 high-pressure spike가 있다. calm window가 dread cue를 억제하는 동안 목표 방향 감각은 유지된다. playtest 3회 중 2회 이상에서 "방금 숨 돌릴 타이밍이었다"는 피드백이 나온다. |
| P1-3 | Stage 3/5/7 set-piece와 새 cue의 충돌 조정 | `Assets/_Project/Scripts/Managers/StageSetPieceDirector.cs:411`, `Assets/_Project/Scripts/Managers/StageSetPieceDirector.cs:436`, `Assets/_Project/Scripts/Managers/StageSetPieceDirector.cs:447`, `Assets/_Project/Scripts/Map/EnemySpawnDirector.cs:139` | Stage 3 ForkLure, Stage 5 SplitPressure, Stage 7 ExitSiege가 EchoReturn/EscapeRelief cue를 가리지 않는다. reinforcement spawn은 death reset/new run에서 남지 않는다. set-piece 이벤트는 alert feed와 priority cue에서 하나의 사건으로만 읽힌다. |
| P1-4 | 목표 안내와 혼란 장치의 난이도 곡선 | `Assets/_Project/Scripts/Map/StageLoopDirector.cs:700`, `Assets/_Project/Scripts/Map/StageLoopDirector.cs:816`, `Assets/_Project/Scripts/UI/GameplayFlowGuideRuntime.cs:254` | stage 1-3에서는 breadcrumb chain이 학습용으로 충분히 직접적이다. stage 5+에서는 corrupted hint가 등장해도 player가 색/움직임/사운드로 진짜 목표와 가짜 흔적을 구분할 수 있다. Flow Guide의 debug성 문구는 플레이 검증 후 필요 시 축소한다. |
| P1-5 | 능력 cooldown/loadout 경제 재밸런싱 | `Assets/_Project/Scripts/Managers/StagePressureDirector.cs:145`, `Assets/_Project/Scripts/Managers/RunLoadoutDirector.cs`, `Assets/_Project/Scripts/Player/PlayerDecoyAbility.cs`, `Assets/_Project/Scripts/Player/PlayerSmokeAbility.cs` | Echo/Decoy/Smoke 중 하나만 정답처럼 느껴지지 않는다. pressure가 오를수록 cooldown 압박은 커지지만 최소 한 가지 탈출 선택지가 남는다. Standard preset 기준 stage 1/3/5 연속 플레이에서 ability 사용 분포가 한쪽으로 70% 이상 쏠리지 않는다. |

## P2 - 폴리시 및 콘텐츠

| ID | 작업 | 주요 참조 | 완료 기준 |
| --- | --- | --- | --- |
| P2-1 | Dummy 시각 자산 교체 1차 | `Assets/_Project/Scripts/Map/EchoPulseVisualDummy.cs`, `Assets/_Project/Scripts/Map/ExitPortalDummy.cs`, `Assets/_Project/Scripts/Map/RoomArchetypeHookDummy.cs`, `Assets/_Project/Scripts/Player/PlayerDummyVisual.cs` | Player, Enemy 5종, Breadcrumb, Stamina, Exit, SafeHaven, Hook sigil, Echo/Relief pulse가 debug square/orb 느낌을 벗어난다. 단, gameplay scale과 collider 기준은 변하지 않는다. |
| P2-2 | 핵심 SFX 레이어링 | `Assets/_Project/Scripts/Managers/AudioManager.cs`, `Assets/_Project/Scripts/Managers/AudioCombatDuckingDirector.cs`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:1553`, `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs:1791` | Echo cast, EchoReturn, EscapeRelief, BreathBroken, ExitUnlocked, ChaseStarted, SetPieceShift가 서로 다른 음색/길이로 구분된다. 같은 10초 구간에 중요한 stinger가 3개 이상 겹치지 않는다. |
| P2-3 | UI 문구와 현지화 톤 정리 | `Assets/_Project/Scripts/UI/GameplayHudRuntime.cs:672`, `Assets/_Project/Scripts/UI/EventFeedbackRuntime.cs:451`, `Assets/_Project/Scripts/UI/GameplayFlowGuideRuntime.cs:259` | 플레이용 HUD는 한국어 중심 또는 의도된 영문 경보 중심 중 하나로 정리된다. `Breadcrumb`, `BREATH FOUND`, `Q 응답`, `Flow Step`처럼 섞인 표현은 용어표를 만든 뒤 통일한다. |
| P2-4 | 접근성/가독성 옵션 | `Assets/_Project/Scripts/UI/DreadScreenOverlayRuntime.cs`, `Assets/_Project/Scripts/UI/DebugOverlay.cs`, `Assets/_Project/Scripts/UI/EventFeedbackRuntime.cs` | 색만으로 위험을 구분하지 않는다. priority cue는 stage intensity가 올라가도 최소 표시 시간과 글자 대비를 유지한다. 화면 흔들림/깜빡임 강도는 옵션 또는 tuning field로 낮출 수 있다. |
| P2-5 | 플레이테스트 로그 템플릿과 판정 기준 | `Assets/_Project/Docs/BASELINE_CALIBRATION_PLAYBOOK.md`, `Assets/_Project/Scripts/Managers/RegressionChecklistRunner.cs:532`, `Assets/_Project/Scripts/Player/PlayerBehaviorTelemetry.cs` | 10분 플레이 3회 기준으로 first death time, stage clear time, ability use cadence, unclear death count, cue confusion note를 기록한다. 회귀 PASS와 체감 품질 PASS를 분리해 다음 밸런싱에서 무엇을 바꿨는지 추적 가능하다. |

## 추천 실행 순서
1. `git status --short`와 Unity Console을 먼저 확인해 다른 작업자의 새 변경을 파악한다.
2. `LostBreadcrumbs/Setup/Build Full Playground`로 씬 wiring을 재생성한다.
3. Standard preset stage 1에서 EchoReturn/EscapeRelief 수동 smoke test를 한다.
4. Compact/Standard/Expansive x stage 1/3/5 matrix와 chase readability regression을 실행한다.
5. `Run Release Soak + Write Report File (Auto)`로 저장/로드/죽음/새 런/게이트를 묶어서 검증한다.
6. P0 실패가 없을 때만 P1 튜닝을 시작하고, P1 튜닝 후 baseline final lock을 다시 잡는다.
