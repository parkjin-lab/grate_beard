# 다음 작업 체크리스트
Updated: 2026-05-17

## P0 - Runtime Validity / Release Gate
1. Unity Editor compile
   - Console compile error가 0개여야 한다.
   - 프로젝트 코드 warning도 0개를 목표로 한다. 현재 CLI 기준 남은 것은 Unity source generator analyzer load warning뿐이다.
   - `GameplayRhythmDirector`, `RuntimeEventSemantic.RhythmShift`, `RegressionChecklistRunner`, `DebugOverlay`, `SampleScene` serialized reference 깨짐을 확인한다.
   - Unity import/compile이 통과하기 전에는 F11, soak, 기능 작업으로 넘어가지 않는다.

2. `SampleScene` scene wiring smoke test
   - `GameplayRhythmDirector`가 존재하고 활성화되어 있어야 한다.
   - `StagePressureDirector`, `ThreatReadabilityDirector`, `AudioDummyLoopRuntime`, `RegressionChecklistRunner`, `DebugOverlay`가 런타임에서 참조를 잡아야 한다.
   - Play Mode에서 rhythm phase/progress/tempo/intensity/pressure multiplier/cycle count가 overlay 또는 로그로 확인되어야 한다.
   - assigned BGM/ambience clip이 있는 경우 dummy fallback runtime이 의도치 않게 자동 재생하지 않는지 확인한다.
   - 작은 Game view에서 DebugOverlay main panel이 스크롤되고 regression panel이 main panel을 덮지 않는지 확인한다.

3. F11 regression
   - 필수 확인: `Rhythm.Enabled`, `Rhythm.PressureShape`, `Rhythm.Telemetry`.
   - 기존 pressure curve, objective loop, chase readability, death reset, save/load, transient reset도 함께 확인한다.
   - 실패 시 release soak보다 먼저 수정한다.
   - save/load 후 현재 rhythm phase와 restored pressure/multiplier가 어긋나지 않는지 확인한다.
   - `MapGenerated` autosave가 새 stage rhythm/pressure 재계산 후 저장되는지 확인한다.

4. Release soak
   - 권장 순서: Auto soak preflight -> trace/status 확인 -> release soak + report file.
   - 보고서에서 save/load, new-run, death reset, matrix, chase readability, rhythm, transient cleanup이 모두 확인되어야 한다.
   - `ReleaseSoak.I#.LoadTransientReset`과 `ReleaseSoak.I#.NewRunTransientReset`이 PASS인지 확인한다.
   - `ReleaseSoak.I#.Save`, `ReleaseSoak.I#.Load`, `ReleaseSoak.I#.NewRun`이 regression mutation guard에 의해 skip되지 않는지 확인한다.

5. Stage 1~3 수동 플레이테스트
   - Calm이 길 읽기/판단 시간을 주는지 확인한다.
   - Build가 "하나 더 먹고 갈까"라는 유혹을 만드는지 확인한다.
   - Spike가 무섭지만 불공정하지 않은지 확인한다.
   - Release가 수치가 아니라 플레이 감각으로 체감되는지 확인한다.

## P1 - Rhythm Feel Tuning
1. 플레이테스트 피드백 정리
   - 버그, 불공정함, 리듬 체감 부족, UI/오디오 과잉 신호를 분리해서 기록한다.

2. Release readability
   - 현재 가장 큰 디자인 갭이다.
   - objective whisper, room tone 완화, fog dread 약화, 짧은 stamina relief, enemy search hesitation 중 하나 이상으로 "숨 돌리는 구간"을 체감시킨다.

3. Build temptation
   - Build 구간에 risk cache, corrupted breadcrumb, exit cache, breadcrumb momentum의 매력을 강화한다.
   - 목표는 플레이어가 안전한 선택을 알면서도 위험한 선택을 고민하게 만드는 것이다.

4. Spike fairness
   - Build 후반 또는 Spike 진입 직전에 읽을 수 있는 tell을 추가한다.
   - Spike 중에는 새롭고 불공정한 문제를 만들기보다 이전 선택의 결과가 증폭되게 한다.

5. Set-piece phase timing
   - stage 3/5/7 set-piece를 Build-late 또는 Spike-entry로 이동하는 방향을 검토한다.
   - P0/F11/release soak 안정화 전에는 보류한다.

## P2 - Feature / Content / Presentation
1. Dummy visual/audio replacement
   - 남은 더미 비주얼을 1차 readable asset으로 교체한다.
   - EchoReturn, EscapeRelief, BreathBroken, RhythmShift, ExitUnlocked, SetPieceShift의 SFX 언어를 분리한다.

2. HUD/localization polish
   - 플레이어 화면에 debug phase name이나 내부 용어가 새지 않게 한다.
   - Korean/English HUD 문구 일관성을 점검한다.

3. Asset/source-control cleanup decision
   - `Assets/Feel`, `Assets/Layer Lab`, `Assets/ThirdParty`, `Assets/_Recovery`를 별도 트랙으로 분류한다.
   - 임의 삭제, 임의 스테이징, `git add .`는 금지한다.

## 권장 실행 순서
`git status --short` -> `git diff --check` -> Unity compile -> Play Mode -> DebugOverlay rhythm telemetry -> F11 -> release soak report -> stage 1~3 playtest -> P1 tuning
