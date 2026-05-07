# LostBreadcrumbs 게임 업데이트 방향 로드맵

Updated: 2026-05-04

## 0) 문서 목적
- 이 문서는 현재 구현된 플레이 루프를 기준으로, 다음 업데이트들이 어디에 집중해야 하는지 정리한 전방향 로드맵이다.
- 기준점은 `PROJECT_STATUS_NEXT_BASELINE`, `BASELINE_CALIBRATION_PLAYBOOK`, `UX_UI_FUN_DIRECTING_STRATEGY_2026-04-08`, 그리고 현재 런타임 시스템이다.
- 핵심 판단: 지금 단계의 병목은 "새 시스템 부족"보다 "플레이어가 위험을 읽고, 선택하고, 결과를 납득하는 완성도"에 있다.

## 1) 현재 게임 기반 요약
- 코어 루프: 순차 스테이지 생성 -> Breadcrumb 수집 -> Exit Portal 해금 -> 탈출/다음 스테이지 진입.
- 생존 도구: 스태미나, 손전등, Echo Pulse, Decoy, Smoke Screen, Safe Haven.
- 위험 구조: 소음, 시야, Fog of War, 벽 관통 감쇠, 적 추적/수색/재획득, 후반 압박.
- 적 기반: Cautious, Flanker, Impulsive, Obsessive, Seeker 프로필과 플레이어 행동 텔레메트리 기반 학습 페이즈.
- 진행 압박: `StagePressureDirector`, `ThreatReadabilityDirector`, stage 3/5/7 set-piece, Compact/Standard/Expansive 맵 프리셋.
- UX/피드백: HUD, Flow Guide, 이벤트 피드, 우선순위 토스트, Death Recap, 오디오 스팅어, 전투 덕킹.
- 안정화 장치: 저장/로드/새 런, 로드아웃 저장, 회귀 체크리스트, preset x stage 매트릭스, chase readability 회귀, release soak harness.

## 2) 디자인 필러
1. 읽히는 위협
   - 죽음보다 먼저 단서가 와야 한다. 소리, 시야, 안개, 카메라, 적 마커, HUD는 같은 위험을 서로 다른 언어로 반복해 알려야 한다.
   - 단서가 많아지는 방향보다, 한 위험이 "어디서 왔고 무엇을 해야 하는지" 더 빠르게 읽히는 방향을 우선한다.

2. 선택 가능한 은신
   - 플레이어는 숨기만 하는 캐릭터가 아니라 소음과 시야를 거래하는 캐릭터다.
   - Sprint, Echo, Decoy, Smoke, Safe Haven은 각각 이득과 대가가 선명해야 하며, 같은 문제를 다른 방식으로 해결하게 만들어야 한다.

3. 탈출로 완성되는 압박
   - Breadcrumb 수집은 탐색, Exit 해금은 전환, 탈출 동선은 클라이맥스다.
   - 스테이지가 오를수록 단순 수치 상승이 아니라 "안전지대 축소, 거짓 단서, set-piece, 적 조합"으로 압박을 바꾼다.

4. 공정한 적응형 AI
   - 적은 플레이어를 배운다는 느낌을 주되, 억울하게 정답을 아는 것처럼 보이면 안 된다.
   - 학습, 기억, 수색 우선순위, Seeker의 은신/연막 관통은 항상 관찰 가능한 전조와 카운터플레이를 가진다.

5. 검증 가능한 완성도
   - 새 기능은 회귀 체크와 플레이테스트 관찰 기준을 함께 가져야 한다.
   - `F11` 체크, preset x stage 매트릭스, soak, Death Recap, 이벤트 로그는 개발용 장식이 아니라 업데이트 승인 기준이다.

## 3) 단기 업데이트 방향 (1~2 스프린트)
목표: 릴리즈 후보 안정화와 첫 10분 가독성 확보.

### P0-1. Release Candidate Soak 실행 및 트리아지
- `Run Release Candidate Soak Pass` 또는 `F2`로 반복 save/load/death-reset/new-run/matrix gate를 실행한다.
- 실패는 기능별로 새 이슈를 늘리지 말고, `Soak Actions`, `Soak Iterations`, detailed report 기준으로 재현성 높은 순서부터 처리한다.
- 완료 기준:
  - release checklist gate가 `ready=Y`를 만들 수 있는 상태.
  - final lock baseline이 고정되어 있고 의도하지 않은 threshold drift가 없다.
  - 디스크 저장 억제 상태에서도 soak 로그가 공유 가능한 형태로 남는다.

### P0-2. 더미 자산 교체의 최소 세트 확정
- Player, 기본 Enemy 5종, Breadcrumb, Stamina, Safe Haven, Exit Portal, Hook cue는 실제 프리팹/스프라이트로 교체한다.
- Echo/Decoy/Smoke/Chase/Exit Unlock/Death Recap은 임시 도형이 아닌 공통 VFX 문법을 가진다.
- 완료 기준:
  - 10분 플레이에서 핵심 오브젝트가 이름표나 디버그 텍스트 없이 구분된다.
  - `*Dummy*` 런타임 표현은 개발용 fallback으로만 남고, 일반 플레이 경로의 주 표현이 아니게 된다.

### P0-3. 스테이지 1~3 온보딩 압축
- 첫 스테이지는 이동, 소음, Breadcrumb, Exit 해금만 확실히 가르친다.
- 두 번째 스테이지는 Decoy/Smoke의 차이와 Safe Haven의 의미를 보여준다.
- 세 번째 스테이지는 첫 set-piece와 chase 전환을 "경고 -> 대응 -> 결과" 순서로 보여준다.
- 완료 기준:
  - 신규 플레이어가 3분 안에 목표와 위험 원인을 설명할 수 있다.
  - 첫 죽음 후 Death Recap을 보고 다음 시도에서 바꿀 행동을 말할 수 있다.

### P0-4. HUD와 Flow Guide 정리
- 전투 중 상시 정보는 목표, 체력/스태미나, 위협, 능력 쿨다운으로 제한한다.
- DebugOverlay는 기본 비노출을 유지하고, 개발용 수치가 일반 HUD를 대체하지 않게 한다.
- 우선순위 토스트는 lock-on, chase, exit unlocked, death만 강하게 남기고 routine objective feed는 과밀하지 않게 유지한다.
- 완료 기준:
  - 16:9, 21:9, 노트북 해상도에서 HUD/Flow Guide/Death Recap이 겹치지 않는다.
  - 한국어/영어가 섞인 주요 플레이 문구는 의도된 용어 외에는 정리된다.

## 4) 중기 업데이트 방향 (3~6 스프린트)
목표: 반복 플레이의 변주와 빌드 품질을 함께 확장.

### M1. 스테이지 티어별 정체성 강화
- stage 1~2: 학습과 여유. 위험은 낮고 단서가 선명하다.
- stage 3: 첫 signature beacon set-piece. "탈출로가 열린 뒤 더 위험해진다"는 규칙을 체감시킨다.
- stage 5: Safe Haven 축소, 거짓 Breadcrumb echo, Seeker 비중 증가로 안정 루트를 흔든다.
- stage 7+: set-piece 조합, 적 프로필 믹스, 장기 런 보상/위험을 본격화한다.
- 완료 기준:
  - 플레이어가 스테이지 숫자만 보고도 기대되는 위험 리듬을 구분할 수 있다.
  - 같은 map preset 안에서도 stage tier별 기억점이 생긴다.

### M2. 적 프로필의 역할 언어 정리
- Cautious: 느리지만 오래 추적하고 단서를 놓치지 않는 압박.
- Flanker: 정면 추격보다 우회 경로와 차단을 담당.
- Impulsive: 빠른 반응과 과한 추격으로 즉각적 공포를 담당.
- Obsessive: 하나의 단서에 집착해 Safe Haven 주변 압박을 만든다.
- Seeker: 은신/연막 카운터 역할. 단, 전조와 약점이 반드시 드러나야 한다.
- 완료 기준:
  - 각 적은 실루엣, 소리, 마커, 이동 리듬만으로 구분된다.
  - Decoy/Smoke/Echo 중 최소 하나는 각 적에게 강하거나 약한 선택지가 된다.

### M3. 로드아웃을 실제 진행 보상으로 연결
- Balanced, Pathfinder, Echo Specialist, Shadow Runner를 단순 디버그 선택이 아니라 플레이 스타일 선택으로 정리한다.
- 해금 조건은 "특정 스테이지 도달", "소음 낮은 클리어", "Echo 적극 사용", "추격 회피"처럼 시스템 숙련과 연결한다.
- 저장 데이터와 UI는 이미 기반이 있으므로, unlock presentation과 선택 화면의 명료성을 먼저 만든다.
- 완료 기준:
  - 각 로드아웃은 장점/약점이 1문장으로 설명되고, 1회 플레이에서 체감된다.
  - 특정 로드아웃이 항상 정답이 되지 않도록 pressure economy와 쿨다운 배율을 함께 검증한다.

### M4. 맵 아키타입 콘텐츠 확장
- Room/Fork/Hideout/Corridor/Risk cell의 기능 차이를 시각 자산과 상호작용으로 강화한다.
- Hook cue는 방 종류별 소리/위험/보상의 작은 문법으로 발전시킨다.
- Safe Haven은 단순 회복 지대에서 "잠깐 숨을 곳", "위험한 숨을 곳", "Seeker에게 들킬 수 있는 곳"으로 변주한다.
- 완료 기준:
  - 미니맵이나 디버그 뷰 없이도 방 종류가 플레이 감각으로 읽힌다.
  - Compact/Standard/Expansive 모두에서 Breadcrumb, Safe Haven, 적 스폰이 유효한 동선을 만든다.

## 5) 장기 업데이트 방향 (Vertical Slice 이후)
목표: 데모/얼리 액세스 후보로 전환 가능한 콘텐츠 파이프라인 확보.

- 내러티브 레이어: Breadcrumb가 단순 수집물이 아니라 장소 기억, 왜곡된 안내, 탈출 의지를 담는 단서가 되게 한다.
- 챕터 구조: stage tier를 챕터/구역 테마로 묶고, 각 구역에 고유 적 조합과 set-piece를 둔다.
- 접근성: 색 의존 경고에 보조 패턴/아이콘/오디오를 제공하고, 카메라 흔들림/플래시 강도 옵션을 둔다.
- 입력/플랫폼: 키보드 기준이 안정된 뒤 컨트롤러, 리바인딩, Steam Deck급 해상도 검증을 추가한다.
- 성능/콘텐츠 파이프라인: fog texture, VFX pool, enemy echo, generated map bounds를 대형 스테이지 기준으로 예산화한다.
- 출시 운영: 빌드 버전, 세이브 호환성, 회귀 로그 보관, 핫픽스 기준, 플레이테스트 설문을 릴리즈 프로세스로 고정한다.

## 6) 영역별 우선순위

### 콘텐츠
- 스테이지 1/3/5/7의 대표 경험을 먼저 완성한다.
- Breadcrumb, Exit, Safe Haven, Hook, set-piece는 "무엇인지", "왜 위험한지", "어떻게 대응하는지"가 각각 한 번씩 선명하게 드러나야 한다.
- 새 스테이지 수보다 같은 스테이지를 다시 해도 동선과 위협 배치가 납득되는지를 우선한다.

### UX
- 목표와 위협은 짧게, 디버그 수치는 접을 수 있게, 실패 원인은 즉시 보여준다.
- Death Recap은 `cause + pressure + missed option` 구조를 유지하되, 실제 한국어 카피로 다듬는다.
- Flow Guide는 온보딩 도구로 유지하고, 숙련 플레이에서는 점진적으로 축소되거나 옵션화한다.

### AI
- 학습형 AI는 "나를 읽었다"보다 "내 반복 습관이 위험해졌다"로 느껴져야 한다.
- Chase transition, disengage cue, re-acquire hysteresis는 공정성의 핵심이므로 밸런싱 때 마지막까지 회귀 체크한다.
- Seeker 같은 카운터 적은 강하게 만들수록 전조, 약점, 보상도 같이 강화한다.

### Audio
- 현재 dummy tone/fallback loop는 빠른 검증에 유용하지만, 릴리즈 후보에서는 실제 SFX/BGM/ambience clip이 기본이어야 한다.
- 소리 문법:
  - Echo: 정보와 위험을 동시에 알리는 넓은 파형.
  - Decoy: 적을 유혹하는 반복 신호.
  - Smoke/Safe Haven: 위험을 낮추는 저역/흡음 계열.
  - Chase/Exit Unlock: 즉시 행동을 요구하는 짧은 스팅어.
- ducking은 위협을 명확히 하되, Breadcrumb/Exit 관련 중요한 정보음을 묻지 않아야 한다.

### Visual
- 안개, 카메라, 적 마커, hook sigil은 같은 pressure curve를 공유해야 한다.
- 핵심 오브젝트는 색만이 아니라 실루엣과 움직임으로도 구분한다.
- 후반부 visual noise가 늘어도 Exit, 적 상태, 능력 쿨다운은 항상 최상위 가독성을 가져야 한다.

## 7) 검증 전략

### 자동/반자동 검증
- 기본: Unity Console compile clean.
- `F11`: map generation, pressure wiring, death reset, preset x stage matrix, chase readability 회귀 확인.
- Matrix 대상: Compact/Standard/Expansive x stage 1/3/5.
- Baseline: final lock policy 적용 후 frozen 상태 유지.
- Soak: release candidate soak 5회 이상 반복을 목표로 하고, save/load/new-run/death-reset/matrix gate가 모두 통과해야 한다.
- 로그: detailed soak report를 공유 가능한 파일로 남기고, failure digest는 우선순위 산정에 사용한다.

### 플레이테스트 검증
- 첫 10분 관찰:
  - 첫 Breadcrumb까지 걸린 시간.
  - 첫 Exit unlock까지 걸린 시간.
  - 첫 죽음 원인 설명 가능 여부.
  - Echo/Decoy/Smoke 각각의 사용 여부.
  - Safe Haven을 의도적으로 사용했는지 여부.
- 반복 플레이 관찰:
  - stage 1 -> 3 이탈률.
  - stage 5 이후 "불공정하다"와 "긴장된다" 피드백 비율.
  - chase disengage가 성공/실패 모두 납득되는지.
  - 로드아웃별 클리어율과 능력 사용 편향.

### 밸런스 검증
- early stage는 압박 상한을 유지하고, 실수 학습 시간을 보장한다.
- mid stage는 set-piece와 적 조합으로 기억점을 만든다.
- late stage는 safe haven/stamina/breadcrumb pressure를 줄이는 대신, 억울한 즉사나 과밀 HUD를 만들지 않는다.
- 기준 변경이 필요하면 baseline을 임의 갱신하지 말고, 의도와 플레이테스트 근거를 기록한 뒤 재락/재동결한다.

## 8) 릴리즈 준비 기준

### 기능 안정성
- Unity Console에 에러가 없다.
- save/load/new-run/death-reset이 반복 soak에서 깨지지 않는다.
- release checklist gate가 `ready=Y`이며, final lock/matrix/chase/soak 조건이 모두 통과한다.
- debug-only hierarchy나 probe가 일반 빌드 경로에 노출되지 않는다.

### 콘텐츠 완성도
- 일반 플레이 경로의 핵심 오브젝트와 주요 피드백은 더미 표현을 벗어난다.
- stage 1/3/5는 최소 기준 플레이 경험이 완성되어 있고, stage 7은 방향성을 보여주는 signature beat가 있다.
- Compact/Standard/Expansive 모두에서 길 찾기, 목표 수집, 탈출 루프가 성립한다.

### UX/가독성
- 첫 플레이어가 목표, 위험, 주요 조작을 외부 설명 없이 이해한다.
- Death Recap이 다음 행동을 제안하고, "왜 죽었는지 모르겠다" 피드백이 주요 반복 이슈로 남지 않는다.
- HUD/토스트/Flow Guide가 주요 해상도에서 겹치지 않는다.

### 밸런스/공정성
- 적의 학습과 Seeker의 카운터 성능은 강하지만 전조와 대응책이 있다.
- Echo/Decoy/Smoke는 각각 쓰이는 상황이 있으며, 하나의 능력이 모든 문제를 해결하지 않는다.
- late-stage pressure는 긴장을 만들되, early-stage 학습 곡선을 침범하지 않는다.

### 오디오/비주얼 품질
- 핵심 SFX와 스팅어가 실제 클립으로 배치되어 있고 fallback tone은 개발용 안전망이다.
- fog/camera/readability 효과가 pressure curve와 맞물리며, 후반부에도 정보 우선순위가 무너지지 않는다.
- 색각 의존도가 높은 경고에는 모양, 움직임, 소리 보조 단서가 있다.

## 9) 업데이트 판단 원칙
- 새 기능 추가보다 먼저 "읽힘, 선택, 결과, 검증"을 통과했는지 본다.
- 플레이어가 실패를 설명하지 못하면 밸런스 문제가 아니라 커뮤니케이션 문제로 먼저 다룬다.
- 디버그 패널에서만 좋은 시스템은 아직 게임이 아니다. HUD, 소리, 시각 단서, Death Recap으로 전달될 때 업데이트 완료로 본다.
- 로드맵 변경은 가능하지만, baseline/soak/playtest 근거 없이 threshold만 바꾸는 업데이트는 릴리즈 후보에 포함하지 않는다.
