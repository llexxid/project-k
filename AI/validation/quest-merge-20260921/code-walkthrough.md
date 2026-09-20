# 병합 후 퀘스트 코드 흐름

이번 변경은 퀘스트 UI와 저장 개선을 합치는 작업이다. 새 관리 계층이나 표시 전용 타이머를 만들지 않고 기존 객체의 책임을 유지했다.

## 생성과 화면 연결

`Assets/_Project/Scripts/Quest/QuestManager.cs`는 bootstrap 씬이 생성한다. `Awake()`에서 singleton을 지정하고 씬 전환에도 유지한다. `OnEnable()`에서 `LocalProgression.Changed`와 `AccountChanged`를 구독하고 `OnDisable()`에서 해제한다. 기존 `Start()`/기간 동기화는 저장 초기화를 담당하고, `GetSnapshot()`은 파일에 접근하지 않는다.

`Assets/UGUI/Scripts/Views/BalanceQuestPanel.cs`는 UIManager가 연 가이드 패널에 연결된다. `Bind()`가 프리팹 필드와 탭을 연결하고 `Connect()`가 QuestManager를 구독한다. 패널이 다른 메뉴에 가려지면 `OnDisable()`에서 이벤트만 해제한다. 돌아왔을 때 같은 패널의 탭 선택과 행을 재사용하고 다시 구독한다.

`Assets/UGUI/Prefabs/Panels/Panel_Guide.prefab`과 `Items/Item_GuideStepRow.prefab`의 배치와 컴포넌트 연결은 feat/quest의 것을 유지했다. develop의 에셋 정리에 따라 스프라이트 GUID만 알려진 프로젝트 작업본으로 옮겼다. 폰트는 develop의 유효한 원본 참조를 채택했다.

## 집계, 저장, 표시

`Assets/_Project/Scripts/Core/Balance/ProgressionState.cs`는 저장할 데이터와 `DeepClone()`을 제공한다. 퀘스트 완료 집합과 보류 보상의 목록 및 각 보상 객체까지 복제하므로 거래가 실패해도 원본에 변경이 새지 않는다. 저장 스키마 번호는 바꾸지 않았다.

`LocalProgression.Execute()`는 기존 자동 저장을 먼저 마무리하고 복제본을 만든다. `BattleEconomy.PrepareQuestTime()`이 이전 기간 전투 시간을 먼저 반영하고, `QuestEconomy.Before()` → 실제 변경 → `After()` → 검증 → `WriteSnapshot()` 순서로 실행한다. 저장 성공 후에만 `_state`를 교체하고 전투 시간 버퍼를 확인 처리한다. 파일 교체와 변경 알림은 각각 하나의 함수에 모았다.

`Assets/_Project/Scripts/Core/Balance/ProgressionAutosave.cs`의 정상 스킬 집계는 메모리에 모이고 `TickSkillCounters()`가 0.75초 간격으로 저장을 시작한다. 작업 스레드는 독립적인 복제본과 파일 경로만 받는다. 저장 중 추가 시전이 생기면 순번이 달라져 dirty 상태가 남고 다음 저장에 포함된다. `ProgressionAutosave`는 첫 런타임 집계 때 생성되어 Update에서 저장을 진행하고 pause/quit에서 flush한다.

기간 경계의 첫 시전은 이전 전투 시간과 함께 기존 `Execute()` 거래를 사용한다. `RecordSkillCast()`는 집계 승인 여부를 bool로 돌려준다. `Assets/MageTower/Scripts/MageTowerManager.cs`는 이 값이 false이면 효과와 쿨다운을 시작하기 전에 반환한다. 디스크가 실패한 경계에서 실제 시전만 실행되고 집계가 빠지는 일을 막기 위한 작은 연결이다. 이 경우 저장이 복구될 때까지 시전은 거절되며, 정상 기간의 시전에는 동기 저장을 추가하지 않는다.

자동 저장이 끝난 직후 다른 거래가 거절되더라도 `Execute()`의 `finally`에서 이미 완료된 저장의 `Changed`를 발행한다. `QuestManager`는 이 이벤트로 dirty 상태가 된 뒤 `LateUpdate()`에서 스냅샷을 비교하고 `QuestsChanged`를 보낸다. UI는 변경된 행만 다시 표시한다. 저장과 표시가 같은 성공 알림을 사용하므로 디스크가 느리면 표시도 늦어진다. 0.75초는 지연 상한이 아니다.

## 보상과 이동

받기 입력은 UI → `QuestManager.TryClaim(token)` → `QuestEconomy.TryClaim(token)` → `LocalProgression.Execute()`로 이어진다. 표시 당시 계정 세대, 퀘스트 ID, 기간을 검증하고 보상 지급과 수령 기록을 함께 저장한다. 성공 토스트는 입력을 받은 UI에서 한 번 표시한다. 현재 기간의 수령한 행은 전체 게이지와 비활성 완료 버튼으로 남고, 이전 기간 보류 보상은 수령 후 목록에서 제거된다.

동적 골드는 `StageCatalogRules.MainEnemy`를 사용해 확장 스테이지를 계산한다. 기존 고정 골드 보상은 이관 시 유지하고, 새 동적 보류 보상은 실제 수령 시 계산한다.

`Assets/UGUI/Scripts/Views/QuestNavigation.cs`는 카드와 HUD의 공용 이동 경로다. `Navigate(objective, targetId)`가 소환·장비·전직의 최초 탭을 지정하고 기존 패널 스택에 목적지를 연다. 전투 목표는 패널을 닫아 전투로 돌아간다. `UIManager.ReactivatePanel()`은 같은 목적지가 중첩되어 정적 컨트롤러 연결이 바뀐 경우에만 기존 패널을 다시 연결한다.

## 계정과 복원

`LocalProgression.Open()`은 `_opening`으로 재진입을 막고 이전 계정의 전투 시간과 스킬을 저장한다. 이후 새 계정을 읽어 퀘스트·마법 이관을 적용하고 검증·저장한다. 그 뒤에 계정 참조와 `AccountGeneration`을 바꾸고 알림을 보낸다. 실패하면 이전 계정 참조와 세대를 유지한다.

`Assets/_Project/Scripts/Core/Manager/UserManager.cs`는 장비 복원을 `EquipmentManager.ImportLegacy()`에 맡긴다. 복원 자체는 신규 획득이 아니므로 퀘스트 획득 횟수를 올리지 않는다. 실제 `Grant()` 지급의 집계는 유지한다.

## 다음 변경 지점과 학습 인계

새 퀘스트 목적지나 목표별 탭은 `QuestNavigation`에서 확장한다. 지급 정책은 `QuestEconomy`, 파일 저장 정책은 `LocalProgression`, 표시 배치는 `BalanceQuestPanel`과 행 프리팹이 담당한다. 실제 기기에서 표시 지연이 거슬리는 것이 확인되면 그때 메모리 진행 알림과 내구 저장 알림을 분리한다. UI 조회·수령 API 경계는 그대로 둘 수 있다.

이번 검사는 실패한 거래의 원본 보존, 저장 중 새 집계, 기간·계정 전환, 중복 수령, 완료 행, 프리팹 참조와 메뉴 복귀를 보호한다. Android 전투 성능과 안전 영역은 연결 기기가 없어 미검증이다. 구현 검증은 완료 보고의 결과를 기준으로 하며, 사용자의 코드 이해 확인은 아직 진행 전이다.

설명을 읽고 확인할 질문:

1. `QuestManager.GetSnapshot()`과 `LocalProgression.Execute()`는 각각 어떤 일을 맡으며, UI에서 저장 데이터를 직접 바꾸지 않는 이유는 무엇인가?
2. `ProgressionState.DeepClone()`에서 `PendingQuests`의 각 보상 객체까지 복제하지 않으면 저장 실패 시 어떤 문제가 생길 수 있는가?
3. 자동 저장 완료 직후 `Execute()`의 mutate가 false를 반환할 때도 `Changed` 알림을 유지해야 하는 이유는 무엇인가?
4. `LocalProgression.Open()`이 이전 계정의 전투 시간·스킬 저장보다 먼저 `AccountGeneration`을 바꾸면 어떤 데이터가 잘못 처리될 수 있는가?
