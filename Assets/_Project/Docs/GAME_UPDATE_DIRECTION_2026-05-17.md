# 게임 향후 업데이트 방향성
Updated: 2026-05-17

## 방향성 요약
현재 업데이트 방향은 "더 강한 압박"이 아니라 "호흡 있는 공포 루프"다. 리듬 시스템은 도입됐지만 아직 검증 대기 상태다. 다음 업데이트는 안정성 검증을 먼저 끝낸 뒤, 각 리듬 페이즈가 플레이어 선택과 보상을 다르게 만들도록 좁게 진행한다.

## 단기 방향
1. 런타임 안정성 증명
   - Unity compile, Play Mode, F11 regression, release soak, stage 1~3 playtest를 같은 워크스페이스 상태에서 확인한다.
   - 이 과정이 끝나기 전에는 새 기능을 추가하지 않는다.

2. Release를 체감 가능한 보상으로 만들기
   - 현재 Release는 수치상 완화에 가깝다.
   - objective whisper, room tone, fog dread, stamina relief, enemy search hesitation 중 하나 이상으로 플레이어-facing relief를 만든다.

3. Build를 욕심의 구간으로 만들기
   - risk cache, corrupted breadcrumb, exit cache, breadcrumb momentum이 Build에서 가장 매력적으로 보이게 한다.
   - 안전한 선택은 명확해야 하지만, 위험한 선택이 충분히 유혹적이어야 한다.

4. Spike를 공정한 시험으로 만들기
   - Spike는 짧고 강해야 한다.
   - Build 후반에 읽을 수 있는 tell을 제공한다.
   - Spike 중 새 문제를 임의로 만들기보다 이전 선택의 결과를 증폭한다.

5. Set-piece를 phase 기반으로 재배치
   - stage 3/5/7 이벤트는 장기적으로 Build-late 또는 Spike-entry에 붙이는 편이 자연스럽다.
   - 단, P0 검증 전에는 보류한다.

## 중기 방향
1. Breadcrumb를 네 페이즈를 관통하는 리듬 척추로 만든다.
2. Risk cache를 Build의 대표 보상/유혹 장치로 강화한다.
3. Exit unlock을 단순 완료 이벤트가 아니라 escape drama 시작점으로 만든다.
4. 적 프로필별 리듬 역할을 분리한다.
   - distant pressure, flank pressure, panic pressure, obsessive pressure, counter-pressure.
5. Dummy visual/audio를 플레이테스트 가능한 readable asset으로 교체한다.

## 운영 기준
- 자동검증과 체감 플레이테스트를 분리해서 기록한다.
- F11/release soak 통과는 필요조건이지 충분조건이 아니다.
- 리듬 변조는 회귀 중 꺼질 수 있으므로 실제 Play Mode 체감 확인이 필요하다.
- 커밋 전에는 `GameplayRhythmDirector.cs`, AI warning cleanup 파일, 2026-05-10/2026-05-17 문서, 관련 `.meta`가 누락되지 않았는지 수동 확인한다.
- 대형 미추적 에셋 폴더는 별도 결정 전까지 스테이징하지 않는다.

## 업데이트 완료 기준
이번 리듬 업데이트는 다음 조건을 만족해야 완료로 본다.
- Unity Console compile error 0개.
- 프로젝트 코드 compile warning 0개. 단, 로컬 Roslyn 직접 실행에서 발생하는 Unity source generator analyzer load warning은 별도 환경 이슈로 분리한다.
- Play Mode에서 rhythm telemetry 확인.
- F11 regression PASS.
- release soak report PASS.
- stage 1~3 플레이테스트에서 Calm/Build/Spike/Release 체감 결과 기록.
- Release 체감 부족 여부에 대한 다음 액션이 문서화됨.
