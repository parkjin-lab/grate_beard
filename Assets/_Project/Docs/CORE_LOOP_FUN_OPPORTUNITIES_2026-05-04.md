# LostBreadcrumbs 코어 루프 재미 기회안

작성일: 2026-05-04  
작성 범위: Worker 3 문서 제안 전용. 코드 변경 없음.

## 0. 읽은 맥락

- `Assets/_Project/Scripts/Map/StageLoopDirector.cs`
- `Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs`
- `Assets/_Project/Scripts/Player/PlayerDummyController.cs`
- `Assets/_Project/Scripts/Player/PlayerEchoPulseAbility.cs`
- `Assets/_Project/Scripts/UI/GameplayHudRuntime.cs`
- `Assets/_Project/Scripts/UI/GameplayFlowGuideRuntime.cs`
- `Assets/_Project/Scripts/UI/EventFeedbackRuntime.cs`
- `Assets/_Project/Scripts/UI/DebugOverlay.cs`
- 보조 확인: `StagePressureDirector`, `PlayerVitalSystem`, `SafeHavenZone`, `StaminaPickup`, `BreadcrumbPickup`, `PlayerDecoyAbility`, `PlayerSmokeAbility`, `EnemySpawnDirector`, 기존 Docs.

## 1. 현재 코어 루프 진단

현재 플레이 루프는 이미 꽤 명확한 골격을 갖고 있다.

1. 맵 생성 후 탐색한다.
2. 빵조각을 모아 출구를 연다.
3. 소음, 시야, 훅, 적 압박을 읽으며 이동한다.
4. 위험하면 Echo Pulse, Decoy, Smoke, Safe Haven, Sprint를 사용한다.
5. 출구가 열리면 탈출하고 다음 스테이지로 넘어간다.
6. 사망하면 현재 스테이지 변형 재생성, 체력/쿨다운/스태미나/안개 리셋, death recap으로 원인을 보여준다.

좋은 기반:

- `StageLoopDirector`가 빵조각, 스태미나 픽업, 안전지대, 출구, 후반 압박, 출구 해금 소음, 빵조각 체인 에코, 오염된 빵조각 에코를 이미 관리한다.
- `ThreatReadabilityDirector`가 근접 위협/스테이지 압박을 합쳐 카메라, 안개, 적 감각, dread beat, phantom cue, close stalker cue, chase disengage 보상, quiet breath, breath snap까지 연결한다.
- `PlayerEchoPulseAbility`는 Q 펄스에 기절, 정찰, 소음 리스크, 잔향, echo return 위협 힌트를 담고 있다.
- `PlayerDummyController`는 이동/스프린트/스태미나/발소리/Space 수동 에코/플래시라이트/temporary noise dampening을 갖고 있다.
- HUD, flow guide, event feedback, debug overlay가 목표/위험/능력/숨 상태/이벤트를 이미 보여준다.

현재 아쉬운 점:

- 기능은 많지만 순간순간 플레이어가 "지금 무엇을 걸고 무엇을 얻는지"가 덜 선명할 수 있다.
- 빵조각 수집이 주 목표지만, 경로 선택의 보상 차이가 아직 약하다.
- Echo와 Quiet Breath는 좋은 재료인데, 하나의 반복 숙련 루프로 명명될 만큼 명확한 계약이 더 필요하다.
- 후반 압박은 잘 올라가지만, 플레이어가 자발적으로 위험을 선택하는 장치가 더 있으면 재미가 커진다.
- 실패 후 리셋은 명확하지만, 다음 시도에서 "방금 실패가 작은 목표로 남는" 회복 루프가 없다.

## 2. 영향/비용 기준

- 영향: 5 = 핵심 루프 체감 변화 큼, 3 = 특정 국면 재미 강화, 1 = 보조적 개선.
- 비용: S = 튜닝/문구/기존 이벤트 연결 중심, M = 새 상태/픽업/규칙 소량, L = 시스템/콘텐츠 확장 큼.
- 우선순위는 영향 대비 비용과 현재 코드 기반 재사용도를 기준으로 정렬했다.

## 3. 우선순위 요약

| 순위 | 제안 | 영향 | 비용 | 추천 이유 |
| --- | --- | ---: | --- | --- |
| 1 | Echo 계약 정리: Scan -> Commit -> Pay | 5 | M | 이미 Space 에코, Q 펄스, 체인 에코, echo return이 있어 가장 빨리 핵심 선택으로 묶을 수 있음 |
| 2 | Quiet Breath를 회복 숙련 루프로 강화 | 5 | S/M | chase disengage 보상이 이미 구현되어 있고, "걷기 vs 다시 뛰기" 선택이 즉시 생김 |
| 3 | 출구 해금 후 Extraction Choice | 5 | M | 현재 출구 해금 소음/비콘이 있으므로 마지막 20초 긴장감을 크게 키울 수 있음 |
| 4 | Risk Room Jackpot | 4 | M | 위험 방/훅/적 스폰 가중치 기반을 보상 선택으로 전환 |
| 5 | Breadcrumb Chain Momentum | 4 | M | 수집 자체를 리듬 게임처럼 만들고, 소음 리스크와 직접 연결 |
| 6 | Enemy Pressure Wave를 명시적 파동으로 | 4 | M/L | 압박 수치가 "분위기"에서 "다가오는 사건"으로 바뀜 |
| 7 | 실패 후 Lost Satchel 회수 루프 | 3 | M | death recap 이후 다음 시도 동기를 부여 |
| 8 | Safe Haven의 stay/leave 선택 강화 | 3 | S/M | 안전지대가 단순 휴식처가 아니라 압박 조절 지점이 됨 |
| 9 | Loadout별 미니 목표 | 3 | M | 기존 로드아웃 경제를 플레이 목표 차이로 확장 |
| 10 | HUD 전술 문장 한 줄화 | 3 | S | 디버그 수치를 플레이어 행동 언어로 바꿔 모든 제안을 받쳐줌 |

## 4. 제안 상세

### 1) Echo 계약 정리: Scan -> Commit -> Pay

핵심:

- Echo를 "공짜 정보"가 아니라 "방향을 얻는 대신 소리를 낸다"는 계약으로 명확히 만든다.
- Space 수동 에코와 Q Echo Pulse의 역할을 분리한다.

현재 재료:

- Space: `PlayerDummyController`가 Echo 소음, 원형 VFX, 안개 reveal trace를 발생시킨다.
- Q: `PlayerEchoPulseAbility`가 기절, 정찰 reveal, 위험 소음, resonance tail, echo return 위협 힌트를 처리한다.
- 빵조각 획득 후: `StageLoopDirector`가 다음 빵조각/출구 방향 chain echo를 만든다.
- chase 후: `ThreatReadabilityDirector`가 objective whisper를 만들 수 있다.

프로토타입:

1. Space Echo는 "짧은 스캔"으로 정의한다.
   - 목표: 가장 가까운 빵조각/출구 방향을 짧게 보여준다.
   - 구현 연결점: `StageLoopDirector.TryGetNextObjectiveTarget`.
   - 리스크: 작은 `NoiseKind.Echo` 또는 `NoiseKind.ItemUse`.
2. Q Echo Pulse는 "위험한 결단"으로 정의한다.
   - 목표: 적 기절 + fog reveal + 숨은 목표 reveal + echo return 위협 힌트.
   - 리스크: 큰 소음, resonance tail로 뒤늦은 적 반응 가능.
3. HUD 문구를 분리한다.
   - Space: `Scan`
   - Q: `Pulse`
   - Echo return 중에는 현재처럼 `Q 응답 0.0m`를 보여주되 "위협 방향"임을 강조한다.

검증 지표:

- 플레이어가 갈림길 앞에서 Space를 쓰는가?
- Q를 도망/기절/정찰의 큰 선택으로 아껴 쓰는가?
- Echo 사용 후 적 반응이 "불공평"이 아니라 "내가 소리를 냈다"로 이해되는가?

주의:

- Space와 Q가 둘 다 파란 원형 VFX라면 역할이 흐릴 수 있다. 색/두께/잔향 시간을 다르게 두는 것이 좋다.

### 2) Quiet Breath를 회복 숙련 루프로 강화

핵심:

- 추격을 끊은 뒤의 2~3초를 "보상 휴식"이 아니라 작은 숙련 구간으로 만든다.
- 플레이어가 걷고 숨을 고르면 더 안전해지고, 바로 뛰면 `BREATH BROKE`가 터진다.

현재 재료:

- `ThreatReadabilityDirector`는 `ChaseStarted`/`ChaseDisengaged` 이벤트를 듣고 `EscapeRelief`를 준다.
- 보상은 스태미나 회복, calm window, dread cue 억제, objective whisper, enemy trail, quiet breath noise dampening이다.
- `PlayerDummyController`는 temporary noise dampening 중 스프린트하면 지속 시간이 빨리 깎이고 `IsTemporaryNoiseDampeningStrained`가 된다.
- breath snap은 소음, 카메라 충격, VFX, `QuietBreathBroken` 이벤트를 이미 갖고 있다.
- HUD는 `숨`, `숨 가쁨` 상태를 스태미나 줄에 표시한다.

프로토타입:

1. Chase disengage 후 2.6초를 "Quiet Breath Window"로 명명한다.
2. 이 시간에 걷거나 멈추면:
   - objective whisper가 조금 더 선명하게 보인다.
   - 다음 Space Echo 소음이 한 번만 감소한다.
   - 또는 스태미나 회복량을 소폭 추가한다.
3. 이 시간에 sprint를 길게 누르면:
   - 현재 breath snap을 유지한다.
   - `BREATH BROKE` priority cue와 작은 소음으로 "회복을 깨뜨렸다"는 결과를 보여준다.

검증 지표:

- 플레이어가 추격 종료 직후 무조건 전력질주하지 않고 1초 이상 멈추거나 걷는가?
- breath snap을 벌점이 아니라 "내가 욕심냈다"로 이해하는가?
- Quiet Breath 후 다음 목표 선택이 빨라지는가?

권장 비용:

- 1차는 튜닝/문구 중심 S.
- 보상 분기를 더 넣으면 M.

### 3) 출구 해금 후 Extraction Choice

핵심:

- 출구가 열리는 순간을 단순 진행 체크가 아니라 "마지막 선택"으로 만든다.
- 지금 탈출하면 안전하지만 보상은 기본. 조금 더 돌아가면 추가 보상이 있으나 적 압박이 올라간다.

현재 재료:

- `StageLoopDirector.UpdateExitState`는 출구 해금 이벤트와 `ExitUnlocked` semantic을 발생시킨다.
- `TriggerExitUnlockPressure`는 출구 위치에 비콘을 찍고, 지연 후 큰 Echo 소음을 낸다.
- HUD/EventFeedback은 `EXIT OPEN - EXTRACT NOW`와 alert feed를 이미 보여준다.
- `ThreatReadabilityDirector`와 `StagePressureDirector`는 압박 상승을 화면/적 감각/쿨다운 경제에 반영할 수 있다.

프로토타입:

1. 출구 해금 후 15~25초짜리 extraction pressure window를 둔다.
2. 선택지 A: 바로 출구로 간다.
   - 안정적 클리어, 추가 보상 없음.
3. 선택지 B: 마지막 보상 캐시를 먹는다.
   - 스태미나/메타 재화/다음 스테이지 시작 이점 중 하나.
   - 대신 exit unlock noise, phantom/close stalker cue, 적 재배치가 더 강해진다.
4. 1차 구현은 새 UI 없이도 가능하다.
   - 출구 방향 chain echo는 초록색.
   - 보상 캐시는 금색 pulse.
   - alert feed는 `EXIT OPEN`만 유지해도 된다.

검증 지표:

- 출구가 열린 뒤에도 플레이어가 1회 이상 "갈까 말까"를 고민하는가?
- 추가 보상을 먹고 죽었을 때 억울함보다 욕심의 결과로 받아들이는가?
- 스테이지 마지막 20초가 초반 탐색과 다르게 느껴지는가?

### 4) Risk Room Jackpot

핵심:

- 위험 방을 피해야 하는 곳만이 아니라 "들어가면 크게 이득인 곳"으로 만든다.
- 현재 `MapCellKind.Risk`, hook chance, enemy risk spawn weight가 모두 있으므로 위험의 기반은 준비되어 있다.

현재 재료:

- `MapSystem`은 Risk 셀에 훅/충돌/오클루더/색을 다르게 둘 수 있다.
- `RoomArchetypeHookDummy`는 사전 경고 후 소음을 낸다.
- `EnemySpawnDirector`는 Risk 셀에 높은 적 스폰 가중치를 준다.
- `StageLoopDirector`는 현재 빵조각 후보를 Corridor/Room/Fork/Hideout 중심으로 뽑고 있다. Risk 전용 보상을 별도 추가하기 좋다.

프로토타입:

1. 스테이지당 0~1개 `Risk Cache`를 Risk 또는 훅 밀집 셀에 배치한다.
2. 보상 후보:
   - 빵조각 2개 가치.
   - Q Echo Pulse 쿨다운 일부 환급.
   - 다음 Quiet Breath 지속 시간 증가.
   - 다음 스테이지 시작 스태미나 보너스.
3. 캐시 근처에는 사전 경고 VFX와 소음 훅을 둔다.
4. 후반에는 Risk Cache 보상은 커지지만 오염된 빵조각 에코 확률도 같이 오른다.

검증 지표:

- 플레이어가 Risk 방 입구에서 멈춰 판단하는가?
- 보상이 너무 강해서 매번 필수가 되지는 않는가?
- 위험 방에서 능력 사용 비율이 증가하는가?

### 5) Breadcrumb Chain Momentum

핵심:

- 빵조각 수집을 단순 카운트업이 아니라 "연속 수집 리듬"으로 만든다.
- 빠르게 이어 먹으면 더 많은 정보/속도/숨 보상을 얻지만, 소음과 오염된 에코 리스크도 커진다.

현재 재료:

- `BreadcrumbPickup`은 수집 이벤트만 발생시킨다.
- `StageLoopDirector.HandlePickupCollected`는 수집 수, objective event, chain echo, 오염된 에코, noise를 처리한다.
- 현재 chain echo는 다음 목표를 알려주고 작은 소음을 낸다.

프로토타입:

1. 5초 안에 다음 빵조각을 먹으면 chain level +1.
2. chain level 보상:
   - 다음 chain echo 지속 시간 증가.
   - 다음 objective target line이 조금 더 길게 보임.
   - 스태미나 소량 회복 또는 footstep noise 짧은 감소.
3. chain level 리스크:
   - chain noise radius 증가.
   - 후반에는 corrupted breadcrumb echo 확률 증가.
4. chain이 끊기면 기본 상태로 돌아간다.

검증 지표:

- 플레이어가 "다음 빵조각까지 밀어붙이기"와 "숨기" 사이를 고민하는가?
- 빠른 플레이가 재미있지만 항상 정답은 아니게 되는가?
- chain noise로 적 반응이 자연스럽게 증가하는가?

### 6) Enemy Pressure Wave를 명시적 파동으로

핵심:

- 현재 압박은 카메라/안개/적 감각/큐로 잘 스며든다.
- 여기에 "곧 적 파동이 온다"는 1~2초 예고와 짧은 고압 구간을 더하면 압박이 기억에 남는 사건이 된다.

현재 재료:

- `ThreatReadabilityDirector`는 dread beat, phantom cue, close stalker cue, flashlight dread, enemy perception multiplier를 이미 조정한다.
- `StagePressureDirector`는 적 수, risk weight, seeker chance, 시작 거리, 능력 쿨다운을 압박에 따라 조절한다.
- `EventFeedbackRuntime`은 priority cue와 flash/camera impulse를 지원한다.

프로토타입:

1. 큰 Echo, 출구 해금, Risk Cache 획득, chain level 3 같은 행동 뒤 `Pressure Wave Pending` 상태를 만든다.
2. 1초 전조:
   - dread beat 1회.
   - close stalker cue 또는 phantom cue.
   - HUD threat banner 색 강조.
3. 5~8초 고압:
   - enemy hearing/suspicion gain 소폭 증가.
   - seeker extra chance 또는 reinforcement 1회.
4. 종료 시:
   - 압박이 살짝 빠지고 Quiet Breath/relief와 연결한다.

검증 지표:

- 플레이어가 전조를 보고 숨거나 도구를 쓰는가?
- 고압 구간이 길어져 피로하지 않은가?
- 파동 종료 후 안도의 피드백이 느껴지는가?

### 7) 실패 후 Lost Satchel 회수 루프

핵심:

- 죽으면 리셋되는 것에서 끝나지 않고, 다음 시도에 작은 회수 목표를 남긴다.
- 실패가 완전한 손실이 아니라 "이번엔 회수하고 빠져나가자"로 바뀐다.

현재 재료:

- `PlayerVitalSystem`은 사망 원인, missed option, 압박 스냅샷, 스테이지를 저장하고 death event를 올린다.
- 사망 후 현재 스테이지를 변형 재생성한다.
- `EventFeedbackRuntime`이 death recap과 팁을 보여준다.

프로토타입:

1. 사망 위치 또는 해당 위치에서 안전하게 떨어진 셀에 `Lost Satchel`을 1개 만든다.
2. 회수 보상:
   - 빵조각 1개 즉시 획득.
   - 또는 스태미나 회복/능력 쿨다운 일부 회복.
3. 회수 위치는 지난 사망 원인에 따라 조정한다.
   - close contact면 더 멀리.
   - ranged hit이면 엄폐 뒤.
4. death recap 마지막 줄에 "Satchel 회수 가능" 같은 짧은 안내를 둔다.

검증 지표:

- 사망 후 재도전에서 플레이어가 첫 목표를 빠르게 이해하는가?
- 회수 시도가 반복 사망을 유발하지 않는가?
- 사망 피드백이 처벌보다 학습으로 받아들여지는가?

### 8) Safe Haven의 stay/leave 선택 강화

핵심:

- 안전지대를 단순 회복 장소가 아니라 "얼마나 오래 머물 것인가"의 선택으로 만든다.

현재 재료:

- `SafeHavenZone`은 은신/노이즈 감소/회복과 후반 unsafe dread, false noise pulse를 가진다.
- `PlayerVitalSystem`은 safe haven 안에서 체력을 회복한다.
- `PlayerConcealmentState`는 진입/이탈 후 짧은 grace concealment를 갖는다.

프로토타입:

1. 안전지대 안에 오래 머물수록 회복은 되지만 false noise 확률이 증가한다.
2. 안전지대에서 회복 tick 1회 후 바로 나가면 `quiet exit` 보너스:
   - 1.5초 footstep noise 감소.
   - 또는 다음 Space Echo 소음 감소.
3. 후반에는 unsafe dread가 "여기도 완전 안전하지 않다"는 신호로 작동한다.

검증 지표:

- 플레이어가 안전지대에서 끝까지 회복할지, 한 틱만 받고 나갈지 선택하는가?
- 안전지대가 과도한 대기 플레이를 만들지 않는가?
- false noise가 불공정한 공격처럼 느껴지지 않는가?

### 9) Loadout별 미니 목표

핵심:

- 로드아웃이 수치 차이를 넘어 "어떤 식으로 스테이지를 풀 것인가"를 바꾸게 한다.

현재 재료:

- `RunLoadoutDirector`와 catalog 기반 로드아웃이 있고, HUD/debug overlay가 선택 상태와 쿨다운 경제를 보여준다.
- Echo/Decoy/Smoke 사용 telemetry가 존재한다.

프로토타입:

1. Pathfinder:
   - 빵조각 chain 유지 보너스.
2. Echo Specialist:
   - Echo return을 정확히 활용하면 쿨다운 환급.
3. Shadow Runner:
   - Quiet Breath를 깨지 않고 출구 도착 시 보너스.
4. Balanced:
   - 기본 루프 유지, 초보 추천.

검증 지표:

- 로드아웃별 능력 사용 패턴이 실제로 달라지는가?
- UI가 로드아웃 목표를 과하게 설명하지 않아도 플레이 중 체감되는가?

### 10) HUD 전술 문장 한 줄화

핵심:

- 현재 HUD와 flow guide는 정보량이 많다. 디버그 숫자를 플레이어 행동 언어로 바꾸는 한 줄이 있으면 위 모든 루프가 쉬워진다.

현재 재료:

- `GameplayHudRuntime`은 HP/스태미나/목표/능력/위험도/alert feed를 표시한다.
- `GameplayFlowGuideRuntime`은 Flow Step, Cooldowns, Controls, Danger, Map Cues를 표시한다.
- `EventFeedbackRuntime`은 priority cue를 띄운다.

프로토타입:

1. 상황별 전술 문장 한 줄만 추가한다.
   - `숨 고르기: 뛰지 말고 다음 목표를 읽어라`
   - `출구 열림: 바로 탈출하거나 보상 캐시를 노려라`
   - `Echo Return: 위협 방향 확인, 직선 돌파 금지`
   - `Risk Cache: 큰 보상, 큰 소음`
2. DebugOverlay에는 수치를 유지하고, 실제 HUD는 행동 문장 중심으로 정리한다.

검증 지표:

- 초회 플레이어가 첫 3분 안에 목표/위험/능력의 관계를 설명할 수 있는가?
- HUD를 오래 읽느라 멈추는 시간이 줄어드는가?

## 5. 추천 1주 프로토타입 순서

1일차: Quiet Breath 튜닝 실험

- chase disengage 후 걷기 보상과 sprint breath snap 빈도를 조정한다.
- HUD 문구는 기존 `숨/숨 가쁨`, priority cue `BREATH FOUND/BREATH BROKE`를 유지한다.

2일차: Echo 역할 분리

- Space는 scan, Q는 commit으로 문구/색/소음 강도를 분리한다.
- Q echo return을 "위협 힌트"로 더 강하게 읽히게 한다.

3일차: 출구 해금 후 extraction window

- 출구 해금 noise/beacon/priority cue를 기준으로 마지막 압박 15~25초를 만든다.
- 추가 보상 캐시 없이도 먼저 "출구 열림이 위험한 순간"인지 검증한다.

4일차: Risk Cache 1개 배치

- 스테이지 3+에서 Risk/Hook 셀 하나에 보상 캐시를 둔다.
- 먹으면 스태미나 또는 쿨다운 환급을 준다.

5일차: Breadcrumb Chain Momentum

- 연속 수집 타이머, chain echo 강화, chain noise 증가를 붙인다.

6~7일차: 3회 소규모 플레이테스트

- Stage 1/3/5 Standard 기준.
- 확인 질문:
  - "왜 죽었는지 알았나?"
  - "Echo를 언제 써야 하는지 알았나?"
  - "출구가 열린 뒤 긴장감이 달라졌나?"
  - "안전지대와 Quiet Breath가 회복 선택으로 느껴졌나?"

## 6. 최소 계측 체크리스트

- Echo 사용 횟수: Space/Q 분리 후 둘 다 쓰이는지.
- Chase disengage 후 2초 내 sprint 비율: Quiet Breath가 작동하면 감소해야 한다.
- Exit unlock 후 탈출까지 걸린 시간: extraction window가 있으면 분산이 커져야 한다.
- Risk Cache 선택률: 30~70%면 선택으로 기능할 가능성이 높다.
- 사망 직전 missed option: Smoke/Decoy/Echo가 반복되면 해당 능력의 읽기성을 보강한다.
- Stage 1/3/5 clear rate: 초반은 유지, 후반은 긴장 상승.

## 7. 다음 코드 작업 시 우선 진입점

- Echo 역할 분리: `PlayerDummyController`, `PlayerEchoPulseAbility`, `StageLoopDirector.TryGetNextObjectiveTarget`, `GameplayHudRuntime`.
- Quiet Breath 강화: `ThreatReadabilityDirector`, `PlayerDummyController`, `GameplayHudRuntime`, `EventFeedbackRuntime`.
- Extraction choice: `StageLoopDirector`, `StagePressureDirector`, `ThreatReadabilityDirector`, `EventFeedbackRuntime`.
- Risk Cache: `StageLoopDirector`, `MapSystem` Risk/Hook 정보, 신규 pickup component 또는 기존 `StaminaPickup` 변형.
- Chain Momentum: `StageLoopDirector.HandlePickupCollected`, `BreadcrumbPickup`, `GameplayHudRuntime`.
- Failure recovery: `PlayerVitalSystem`, `StageLoopDirector`, `EventFeedbackRuntime`.

## 8. 결론

가장 큰 기회는 새 콘텐츠 양을 늘리는 것이 아니라, 이미 있는 시스템을 "선택과 대가"로 더 선명하게 묶는 것이다.  
우선은 Echo, Quiet Breath, 출구 해금 순간을 P0로 잡는 것을 추천한다. 이 세 가지는 현재 구현과 가장 가깝고, 플레이어가 매 스테이지마다 반복해서 겪는 핵심 순간이다.
