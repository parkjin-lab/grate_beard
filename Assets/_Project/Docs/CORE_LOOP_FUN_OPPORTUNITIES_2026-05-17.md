# 핵심 코어 루프 재미 강화안
Updated: 2026-05-17

## 핵심 원칙
현재 Calm/Build/Spike/Release는 압력 곡선으로는 잡혔다. 더 재미있어지려면 각 페이즈가 플레이어의 질문을 바꿔야 한다.

- Calm: 어디로 갈까?
- Build: 조금 더 욕심내도 될까?
- Spike: 내가 한 선택을 지금 수습할 수 있을까?
- Release: 살아남은 덕분에 다음 선택이 얼마나 좋아졌나?

공포는 수치 상승만으로 만들지 않는다. 선택의 의미가 바뀌면서 발생해야 한다.

## Calm - 읽고 준비하는 시간
Calm은 안전한 시간이 아니라 정보가 선명한 시간이어야 한다.

플레이어 선택:
- 안전한 경로로 이동할지, 보상이 커 보이는 경로를 미리 찍어둘지 선택한다.
- 먼 위협의 위치를 파악하고 우회 루트를 계획한다.
- 안정적으로 breadcrumb를 회수하거나 Build 때 노릴 고위험 보상을 기억해둔다.

보상:
- breadcrumb 기본 보상은 작지만 안정적이다.
- 다음 목표 방향 whisper가 가장 명확하다.
- safe haven, exit 방향, risk cache 위치를 힌트 수준으로 보여준다.
- Calm에서 관찰을 잘한 플레이어는 Build에서 더 좋은 선택지를 가진다.

공포:
- 직접 위협보다 예고 중심이다.
- 멀리서 들리는 소리, 흐릿한 움직임, 비정상적인 breadcrumb 신호를 사용한다.

## Build - 유혹과 과속의 시간
Build는 핵심 재미 구간이다. 여기서 플레이어가 "하나만 더"를 선택해야 Spike가 벌칙이 아니라 자기 선택의 결과로 느껴진다.

플레이어 선택:
- 가까운 breadcrumb만 먹고 빠질지, risk cache까지 노릴지 선택한다.
- chain momentum을 위해 빠르게 이동할지, 소음을 줄이며 안정적으로 갈지 선택한다.
- corrupted breadcrumb를 믿을지 의심할지 선택한다.
- exit unlock 직전까지 욕심을 낼지, 미리 탈출 루트를 잡을지 선택한다.

보상:
- breadcrumb 보상이 증가한다.
- 연속 회수 시 route clarity, stamina relief, pressure delay 같은 체감 보상을 준다.
- risk cache는 Build 중 가장 매력적으로 보여야 한다.
- Spike가 가까울수록 보상은 커지고 실패 비용도 커진다.

공포:
- 쫓김보다 잘못된 판단을 할 것 같은 압박이 중심이다.
- 오디오 pitch, fog, breadcrumb wobble, enemy search cue가 점점 강해진다.
- corrupted hint는 Build에서 가장 활발해야 한다.

## Spike - 짧고 읽을 수 있는 시험
Spike는 랜덤 처벌이 아니라 Build에서 한 선택의 결산이어야 한다.

플레이어 선택:
- 도망칠지, 숨을지, decoy/smoke/pulse를 쓸지 선택한다.
- 이전에 파악한 safe route로 꺾을지, 즉흥적인 shortcut을 탈지 선택한다.
- clutch breadcrumb를 먹고 Release를 앞당길지, 생존만 우선할지 선택한다.

보상:
- Spike 중 breadcrumb 회수는 clutch 보상이어야 한다.
- 성공 시 Release가 더 강해지거나 길어진다.
- risk cache를 먹고 살아남으면 다음 Calm에서 더 좋은 정보를 제공한다.
- exit unlock Spike를 넘기면 exit whisper가 강하게 열린다.

공포:
- 위협은 강하지만 규칙은 선명해야 한다.
- enemy tell, 소리 방향, 시야 압박, 카메라/화면 효과가 명확해야 한다.
- 갑작스러운 즉사보다 "내가 탐욕을 부려서 이 상황이 왔다"는 감각이 중요하다.

## Release - 생존 보상과 다음 선택의 씨앗
Release는 단순한 압박 감소가 아니라 생존 보상이어야 한다.

플레이어 선택:
- 회복된 틈에 다음 목표를 읽을지, 바로 momentum을 이어갈지 선택한다.
- safe haven에 들를지, exit/cache 방향으로 재가속할지 선택한다.
- 얻은 정보를 바탕으로 다음 루트를 계획한다.

보상:
- dread audio 완화, fog 완화, objective whisper 강화.
- 짧은 stamina relief 또는 이동 안정감.
- enemy search가 잠깐 느슨해지는 체감.
- 다음 breadcrumb/exit/cache 방향이 더 읽히기 쉬워진다.

공포:
- 완전한 안전이 아니라 "숨 돌릴 수 있지만 오래 머물면 다시 온다"여야 한다.
- Release 말미에 작은 불안 신호로 다음 Build를 예고한다.

## 페이즈별 보상 규칙
| Phase | Breadcrumb | Risk Cache | Corrupted Hint | Exit |
| --- | --- | --- | --- | --- |
| Calm | 작고 안전한 보상, 방향 읽기 강화 | 희미하게 예고 | 거의 조용함, tell 학습 가능 | 먼 방향 whisper |
| Build | chain/momentum 보상 증가 | 가장 선명하고 유혹적 | 가장 활발, 단서도 함께 제공 | unlock 직전 긴장 상승 |
| Spike | clutch 회수 시 압력 완화 또는 Release 가속 | 큰 보상, 큰 실패 비용 | 새 속임수보다 이전 선택의 결과 | unlock noise/chase/escape test |
| Release | 다음 Calm 정보 강화 | 놓친 cache는 약화/소멸 가능 | 잠잠해짐 | exit whisper 선명화 |

## 우선 구현 후보
1. Release 보상 명확화.
2. Build risk cache / breadcrumb momentum 유혹 강화.
3. Spike 사전 tell 추가.
4. exit unlock을 Build crest -> Spike -> Release 흐름으로 연결.
5. set-piece를 stage number가 아니라 rhythm phase에 붙이기.
