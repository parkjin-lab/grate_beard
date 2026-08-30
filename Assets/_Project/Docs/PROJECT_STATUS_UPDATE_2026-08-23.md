# 프로젝트 상황 업데이트
Updated: 2026-08-27

## 이번 슬라이스에서 반영한 것
- Echo Overcharge를 remote에 처음 착륙시켰다. Q 탭은 기존 메아리, 홀드는 충전, 릴리즈는 현재 충전으로 시전, 풀충전은 자동 시전한다.
- 과충전은 정찰/안개 범위와 소음만 키운다. 기절 반경과 지속시간은 그대로다.
- HUD는 홀드 중 `Q 과충전 N%`를 보여 주고, 차지 링은 에코 블루에서 경고 레드로 바뀐다.
- 적 스폰 경로(일반 재생성 + set-piece 증원)에 몸통 반경 충돌 여유를 적용했다.
- 체크포인트 좌표를 검증하고, 맵 밖/벽 안/비정상 좌표면 안전한 생성 칸으로 되돌린다. 세이브 로드도 같은 경로를 쓴다.
- 게임이 일시정지되면(`timeScale <= 0`) 리듬 페이즈 시계가 멈추고, 페이즈 진행도 멈춘다.
- 과충전 텔레메트리와 `Echo.OverchargeContract` 회귀 한 줄을 추가했다.
- 안정화 5: 연막 안에서 메아리를 쓰면 기존 소음 감소를 유지한 채 정찰 반경을 줄인다(기본 0.72배). 과충전은 연막으로 줄어든 소음에 그대로 곱한다. 기절은 그대로다. 이벤트/HUD 문구는 `연막: 짧게 보고 조용히` / `Q 과충전 N% 연막`.
- 플레이어 좌우 보기: 루트 `transform.right` 회전을 제거하고, 이동 입력의 마지막 X로 스프라이트 `flipX`를 켠다. 손전등/미끼/연막도 같은 바라보기 값을 쓴다.
- 프레임 검색 부하: 메아리 정찰과 디버그 훅 캐시가 `FindObjectsByType` 대신 활성 목록을 쓰고, HUD/안개는 `timeScale<=0`에서 멈춘다. 과충전 탭/홀드/연막 동작은 그대로다.
- Release 입장 한 방: Spike→Release 기존 `TryGrantRhythmReleaseRelief`에서 위협 앰비언트가 가라앉고, 땅에 있는 빵가루가 1.25초 한번 빛나며, HUD/알림은 `숨이 트인다`. 페이즈 초/압박/보상 숫자는 그대로다.
- 남은 끊김: 안개가 매 프레임 `FindGameObjectWithTag("Player")`를 하지 않는다. 플레이어는 `ActiveInstance` 캐시만 쓴다. 빵가루 펄스는 공유 틱이며 멀리/안개에 가리면 쉰다. HUD/오버레이/카메라/위협/스폰도 태그 검색을 캐시로 바꿨다. 과충전 탭/홀드/연막은 그대로다.
- 벽/빵 가독: 경계 벽은 `ForestMossyStoneWall`, 빵가루는 `GoldenGlowBreadcrumb`. 바닥에 빵을 쓰거나 벽에 빵을 쓰지 않는다.
- 사망 안개: `resetFogOnDeath`로 탐험 안개는 그대로 지운다. 같은 프레임에 플레이어를 다시 묶고 시야 반경만 파서, 부활 직후 통째로 검게 서 있지 않게 한다.
- 첫 플레이 캠페인: 타이틀 `[시작]` → 프롤로그 이야기책 → 1층. 출구는 `OnStageClear`로 책을 보여 준 뒤 `GenerateNextStage()`를 부른다. 사망은 책을 열지 않는다.
- 커리큘럼: 1층은 Q 탭만, 2층부터 연막과 위험보상, 3층부터 Q 홀드 과충전. 리듬 초/압박 숫자는 그대로다.
- 3층 고요를 기다리며: 2층 클리어 책은 기존 문장, 3층은 순찰/탐색자가 더 많고 세트피스 표식·증원이 한 단계 짙다. 홀드가 처음 열리면 `멀리까지 들릴 수 있다`와 HUD `Q 홀드`. 페이즈 초/압박 배율은 그대로다.
- 4층: 옅은 빵가루만 시간이 지나거나 숲이 핥을 때 지워진다. 위험보상 묶음은 남는다. 3층 책은 기존 문장 뒤 4층. 4층 클리어 책은 `짙은` 묶음 문장.
- 5층: 기존 5층 거짓 가루/늦은 압박을 쓴다. 같은 BreadcrumbPickup을 거짓 길로 심고, Q 홀드는 진짜만 진하게 한다. 거짓 가루는 연쇄/밀도를 받지 않는다. 늦은 압박은 숫자 HUD 없이 나무 안개와 집 불만 짙어진다. 출구가 집 문턱이다.
- 엔딩: 5층 문턱에서 숲/집 실루엣을 먼저 페이드하지 않는다. 그 화면이 마지막 그림이 되고(캡처, 실패 시 마녀 집 아트), 라벨은 `집`, 문장은 `그 집 문턱에서 이야기는 덮였다.` 한 장만 보여 준 뒤 책을 덮고 타이틀로 돌아간다. 5층 긴 페이지/오븐 문장/6층은 없다. 사망은 책을 열지 않는다.
- 가독 아트: 5층/엔딩 책에 마녀 집 그림. 4층 옅은 가루는 작고 창백하고, 5층 거짓 가루는 보라로 흔들리며, 위험보상은 짙은 빵묶음이다. 5층 출구는 문/창 불이지 초록 핑이 아니다.
- 타이틀 이어하기: 체크포인트가 있으면 `시작`과 `이어하기`를 보여 준다. 이어하기는 기존 `SaveManager` 체크포인트로 그 층에 바로 들어가고 이전 책을 다시 읽지 않는다. 새 시작은 여전히 프롤로그 → 1층이며, 엔딩 뒤 체크포인트는 비운다.
- 그림책 타이틀/책장: 타이틀은 기존 펼친 책 프레임 위(`preserveAspect`)에 `헨젤과 그레텔`과 잉크 밑줄 `시작`/`이어하기`를 오른쪽에 둔다. 책 페이지는 같은 CanvasGroup으로 짧게 페이드 인/아웃(0.22s/0.16s)한다. 힌트는 `스페이스로 계속`, 스킵과 그림 없는 텍스트 페이지는 그대로다. 캠페인 규칙/게이트/세이브/사망-무책은 손대지 않았다.
- 책장 소리: `AudioDummyLoopRuntime`이 기존 generated dummy 스타일로 짧은 종이 넘김을 만들어, 책 `ShowPage`와 타이틀 `시작`/`이어하기` 확인에서만 한 번 낸다. 새 오디오 엔진/스팅어 종류/사망-책은 없다.
- 2–4층 클리어 그림: `StorybookSmokeAndCache` / `StorybookHoldAndListen` / `StorybookLickedTrailCache` PNG를 Art와 Resources/Story에 두었고, `TryGetStage2/3/4Illustration`이 클리어 책 왼쪽에 넣는다. 문구는 그대로, 파일이 없으면 텍스트만.
- 숲 빵가루 가독: `FaintLickedBreadcrumb` / `CorruptedFalseBreadcrumb` / `LandmarkTrailCache` PNG를 Art와 Resources/Map에 두었다. `MapReadableArt`가 각각 옅은·거짓·랜드마크 캐시로 로드하고, 없으면 `GoldenGlowBreadcrumb`로 떨어진다. S1 진짜 길은 황금 가루 그대로다. 숫자/캠페인/책 규칙은 손대지 않았다.
- 5층 집 문턱 출구: `HouseThresholdDoorGlow` PNG를 Art와 Resources/Map에 두고, `TryGetHouseThresholdExitSprite` / `ExitPortalDummy.GetHouseGlowSprite`가 쓴다. 빨간 디버그 네모·초록 핑 대신 문/창 불 오두막으로 읽힌다. 파일이 없으면 기존 1×1 흰 픽셀. 출구 언락 숫자·캠페인 규칙은 그대로다.
- S2 연막 가독: `ForestEchoSmokePuff` PNG를 Art와 Resources/Map에 두고, `MapReadableArt.TryGetSmokeSprite` / `PlayerSmokeAbility` 전개 / `SmokeScreenFieldDummy.Awake`가 쓴다. 회색 디버그 원판 대신 숲 안개 뭉치로 읽힌다. 없으면 기존 디버그 원. 반경·수명·시야가림·소음감쇠 숫자는 그대로다.
- Q 메아리 링: `ForestEchoPulseRing` PNG를 Art와 Resources/Map에 두고, `TryGetEchoPulseSprite` / `EchoPulseVisualDummy.GetRingSprite`(`SharedRingSprite`)가 쓴다. 탭·홀드 시전과 과충전 미리보기가 같은 링을 쓴다. 없으면 기존 128px 절차형 고리. 차지 숫자·색·시간은 그대로다.
- 숲 위협 몸: undead 애니 미적용 시 `ForestPatrolThreat` / `ForestSeekerThreat` PNG를 Art와 Resources/Map에서 쓴다. `TryGetPatrolThreatSprite` / `TryGetSeekerThreatSprite` → `EnemySpawnDirector.SpawnEnemy`. 스케일 0.9(콜라이더 0.38 유지). 없으면 기존 틴트 디버그 네모.

## 아직 열린 것
- Unity 6000.5.9f1 에디터 컴파일 확인
- Play Mode에서 patrol/seeker 그림 몸, Q 링, R 연막, 5층 출구·가루 구분을 본다
- 리듬 스냅샷은 여전히 `NO_EVIDENCE`. 페이즈 초/압박/과충전/연막 숫자는 손대지 말 것

## 다음 에이전트
1. Unity 콘솔에서 컴파일만 확인한다.
2. Play Mode에서 undead 없을 때 patrol vs seeker 스프라이트만 확인한다. 숫자를 바꾸지 않는다.

## 한 줄 판정
숲 위협이 undead 없이 순찰/탐색자 PNG로 구분된다.
