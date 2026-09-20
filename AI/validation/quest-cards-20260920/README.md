# 퀘스트 보상 카드 구현·검증 — 2026-09-20

## 결과

Unity 6000.3.21f1에서 실제 `Item_GuideStepRow.prefab`을 수정했다. 보상 아이콘·수량, `종류 · 퀘스트 내용`, 진행바, 우측 행동 버튼을 연결했다. 수동 수령을 유지하며 `진행 중 → 받기 → 지급·저장 성공 → 완료`로 바뀐다. 완료 카드는 목록에 남고 게이지 100%, 낮춘 색상, 비활성 `완료` 버튼을 표시한다.

- 가이드·업적의 수령 완료 행도 조회하며 미해금 후속 단계는 계속 제한한다. HUD의 현재 가이드는 미수령 행 선택을 유지한다.
- 일일·주간 완료 행은 현재 기간 동안 보존하고 기간이 바뀌면 새 토큰으로 교체한다.
- 이전 기간 미수령 행은 기존 만료/지급 정책을 유지한다. 수령 후 사라지는 과거 보관분에 별도 영구 이력은 추가하지 않았다.
- 복수 재화는 두 아이콘/수량으로 표시한다. 현재 카탈로그의 최대 보상 종류는 두 개다. 동적 골드는 확정액 대신 `2분 골드`로 표시한다.
- 직접 이동할 메뉴가 없는 오프라인 보상·일괄 완료 목표는 `진행 중`으로 표시한다. 이동 때문에 보상을 자동 수령하거나 오프라인 보상을 인위적으로 발생시키지 않는다.

## 코드의 실행 흐름

1. `UIManager`가 패널을 만들고 `GuidePanelController.Populate`가 `BalanceQuestPanel.Bind`를 호출한다. 패널은 `QuestManager.QuestsChanged`를 활성화 시 구독하고 숨겨지면 해제한다.
2. `QuestEconomy.GetSnapshot`이 저장된 `Claims`를 읽어 `Claimed` 행과 목표값으로 고정한 진행도를 제공한다. UI 비활성 상태를 별도 저장하지 않으며 저장 스키마 변경은 없다.
3. `BalanceQuestPanel.Rebind`가 선택한 범주의 모든 행을 유지한다. 토큰/구성이 같으면 같은 행 객체를 사용하고, 내용이 바뀐 행만 다시 그린다. 완료 시 정렬을 바꾸지 않아 일일 카드와 스크롤 위치가 유지된다.
4. `GuideStepRowView.SetQuest`는 snapshot을 표시만 한다. `Claimable`과 `Claimed`를 구분하여 전자는 수령 가능, 후자만 완료 비활성으로 표현한다. `IsCompleted`는 두 상태를 모두 포함하므로 버튼 비활성 판정에 사용하지 않는다.
5. `BalanceQuestPanel.Act`가 우측 버튼 입력을 받는다. 진행 중에는 `QuestNavigation.Navigate`, 달성 상태에서는 `QuestManager.TryClaim`을 호출한다. 화면에 바인딩된 기간·계정 토큰을 유지하며 완료/잠금 입력은 아무 작업도 하지 않는다.
6. 지급·저장 결과가 확정된 snapshot을 다시 읽어 완료 표시를 갱신한다. 저장 실패 시 수령 가능한 상태와 같은 토큰을 유지한다.
7. `QuestNavigation`은 HUD와 카드가 공유한다. 메뉴는 기존 스택에 쌓아 뒤로가기로 퀘스트에 복귀하며 전투 목표는 패널을 닫는다. 마탑과 환생은 실제 기존 팝업 진입점을 호출한다.
8. `QuestCardPrefabBuilder.Apply`는 Unity의 PrefabUtility로 기존 프리팹을 부분 수정한다. 체크 버튼 컴포넌트를 우측 행동 버튼으로 재사용하고 이전 필드를 보존했다. `GuidePanelPrefabGens`도 같은 업그레이드를 사용한다.

변경한 런타임 파일: `QuestEconomy.cs`, `BalanceQuestPanel.cs`, `GuideStepRowView.cs`, `GuideGoalView.cs`, 신규 `QuestNavigation.cs`.
에디터/자산: `QuestCardPrefabBuilder.cs`, `GuidePanelPrefabGens.cs`, `Item_GuideStepRow.prefab`. 검증은 기존 `QuestTabAcceptance`/`QuestUiAcceptance`를 확장하고 `QuestCardCapture`/`QuestCardInteraction`을 추가했다. 신규 C#의 `.meta`는 Unity에서 생성했다.

## 실행한 검증

| 검사 | 결과 | 기록 |
|---|---:|---|
| Unity 컴파일 | 오류 없음 | `final-editor-state.json` |
| 퀘스트 지급·카탈로그 | 73 통과 | `core-regression.json` |
| 시간·기간 처리 | 24 통과 | `core-regression.json` |
| snapshot·구형 API 호환 | 31 통과 | `core-regression.json` |
| 실제 프리팹 탭/완료 카드 | 48 통과 | `playmode-cards.json` |
| 실제 raycast/포인터·스크롤·복귀 | 27 통과 | `pointer-input.json` |
| 직렬화 참조/생성기 반복 실행 | 누락 스크립트 0, 참조 연결 정상, 재실행 파일 동일 | `prefab-checks.json` |

총 203개 assertion이 통과했다. 포인터 검사는 운영 계정이 없는 빈 씬의 격리 QA 계정에서 실제 UIManager/프리팹/EventSystem을 사용했다. 기기의 손가락 입력을 검증한 것은 아니다.

완료 후 재접속, 기간 초기화, 업적 다음 단계 해제, 완료 재클릭, 계정 전환 뒤 오래된 버튼 입력, 저장 실패 재시도, 탭 재활성화와 화면 복귀를 확인했다. 완료 행에서 progressFill의 표시 비율과 버튼의 비활성 상태를 검사했다.

## 캡처와 자체 피드백

- `before-weekly-360x800.png`: 변경 전 실제 프리팹의 고정 예시 렌더.
- `first-pass-weekly-360x800.png`: 첫 적용 결과. 기본 UI sprite의 투명 여백/둥근 모서리가 바를 흐리게 하여 기각했다.
- `after-weekly-360x800.png`, `after-daily-540x960.png`, `after-weekly-900x1200.png`: 평면 Image의 anchor 폭으로 수정한 최종 렌더. 진행 중/수령 가능/수령 완료를 고정 예시로 비교한다.
- `live-{achievement,daily,weekly}.png`: 343×609 GameView의 실제 UI 입력 검사 중 캡처. 진행/완료 수치는 격리 QA 계정의 실제 snapshot이다.
- 세 비율 캡처는 같은 Editor의 PreviewScene에서 얻은 결과다. 여러 실제 기기의 검증이 아니다. 검정 배경의 격리 화면이므로 전투 배경의 시인성, 실제 안전 영역, Android 터치/백그라운드 복귀는 미검증이다.

입력 검사 첫 실행은 검증 도구의 `Click`이 비활성 버튼을 무조건 거절하여 18개 검사 후 중단됐다. 제품 코드가 아닌 검사 도구를 보완해 비활성 버튼에도 포인터 이벤트를 보낸 뒤 무동작을 확인했다. 실패 원본은 `pointer-input-initial-failure.json`에 보존했다. 복사한 촬영 도구의 이전 출력 경로도 새 기록 디렉터리로 고쳤고, 잘못 덮어쓴 과거 스크린샷은 수정 전 Git 내용으로 복원했다.

초기 CLI 캡처 시도는 eval이 파일 전체 클래스/using 구문을 받지 않아 실패했다. 에디터 전용 캡처 클래스를 정상 컴파일하는 경로로 전환했다. 실패한 호출을 Unity 검증 통과 횟수에 포함하지 않았다.

## 크기·비용과 남은 검증

- 카드 높이 192 reference px. 기준 가로폭 1080에서 360px 폰의 카드 높이는 64px이다. 행동 버튼은 168×92에 상하 18px raycast 여유를 더해 약 56×42.7px 입력 영역을 갖는다.
- 신규 텍스처/폰트/머티리얼 자산 0개. 기존 카탈로그 재화 sprite와 폰트를 재사용하고 바는 텍스처 없는 평면 Image로 표시한다. 외부 이미지 생성 호출/비용 0.
- 기본 프리팹 활성 Graphic 10개. 실제 수는 복수 보상/이전 기간 표시 여부에 따라 달라진다. 완료 행 보존으로 가이드·업적 목록의 객체 수는 늘 수 있으나 진행 수치가 바뀔 때만 다시 표시한다.
- snapshot 조회 10,000회: 0.0711ms, 할당 0 bytes(`core-regression.json`). 이는 Editor 내 캐시 조회 측정이며 UI/GPU 프레임 성능이나 변경 전후 실기기 벤치마크가 아니다.
- `adb devices -l`: 연결된 기기 없음. Android 빌드/설치·실기기 검증은 수행하지 못했다.
- 원래 `bootstrap.unity`로 복원했다. 종료 시 EditMode, 컴파일 오류 없음, 씬 dirty=false, runInBackground=false를 확인했다. 기존 Console은 지우지 않았다.
- 사용자 기존 변경을 보존했으며 staging·commit·push·PR은 하지 않았다.

## 학습 인계

핵심은 저장된 도메인 상태와 UI 표현을 분리하고, 확정된 지급 결과만 화면에 반영하는 것이다. UI에 별도의 완료 bool을 저장하면 저장 상태와 불일치할 수 있다. 이동 규칙은 실제로 HUD/카드 두 곳에서 필요하므로 작은 공통 함수로 분리했으며, 아직 별도 서비스 인터페이스나 전체 UI 프레임워크를 도입하지 않았다.

보상 종류가 두 개를 넘거나 완료 이력을 여러 기간 보관하게 될 때 각각 보상 슬롯 목록화/별도 이력 보존을 검토한다. 현재 범위에서는 추가 저장 스키마와 목록 가상화를 도입하지 않았다.
